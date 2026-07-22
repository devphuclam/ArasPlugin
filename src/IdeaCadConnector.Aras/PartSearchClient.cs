using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Searches Parts via OData ($filter / $expand / $select) using the Aras
    /// OData endpoint. Requires an OAuth bearer token obtained during IOM login.
    /// </summary>
    public sealed class PartSearchClient
    {
        private const string PartODataPath = "server/odata/Part";

        private readonly HttpClient _httpClient;
        private readonly ArasClientOptions _options;
        private readonly ILogger<PartSearchClient> _logger;

        public PartSearchClient(HttpClient httpClient, ArasClientOptions options, ILogger<PartSearchClient> logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PartSearchClient>.Instance;

            if (options.BaseUri != null)
                httpClient.BaseAddress = options.BaseUri;

            if (options.Timeout > TimeSpan.Zero)
                httpClient.Timeout = options.Timeout;

            // Aras OData endpoint rejects non-minimal metadata formats such as
            // application/json;odata=verbose. Force the standard JSON accept header.
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void SetBearerToken(string token, string tokenType = "Bearer")
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(tokenType ?? "Bearer", token);
        }

        public async Task<(IReadOnlyList<PartSearchResult> Items, int TotalCount)> SearchAsync(
            PartSearchRequest request,
            CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var keyword = (request.Keyword ?? "").Trim();
            var maxResults = request.MaxResults <= 0 ? _options.DefaultMaxSearchResults : request.MaxResults;
            var skip = request.Skip < 0 ? 0 : request.Skip;

            _logger.LogDebug("OData part search keyword='{Keyword}' top={MaxResults} skip={Skip}", keyword, maxResults, skip);

            var query = BuildQuery(keyword, maxResults, skip);
            var uri = PartODataPath + query;

            try
            {
                using var response = await _httpClient.GetAsync(uri, ct);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorCode = (int)response.StatusCode switch
                    {
                        401 => ArasErrorCode.AuthExpired,
                        >= 500 => ArasErrorCode.ServerUnavailable,
                        _ => ArasErrorCode.UnexpectedServerError
                    };

                    throw new ArasOperationException(
                        errorCode,
                        $"OData part search failed: HTTP {(int)response.StatusCode}. {body}",
                        retryable: (int)response.StatusCode >= 500);
                }

                return ParseResponse(body);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OData part search request failed");
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "Part search request failed: " + ex.Message,
                    innerException: ex);
            }
        }

        // ---- Private helpers ---------------------------------------------------

        private static string BuildQuery(string keyword, int maxResults, int skip)
        {
            var filter = string.IsNullOrEmpty(keyword)
                ? ""
                : $"(contains(item_number,'{EscapeODataString(keyword)}') or contains(name,'{EscapeODataString(keyword)}'))";

            var query = $"?$select={CadSelectFields.PartSearch}";

            if (!string.IsNullOrEmpty(filter))
                query += "&$filter=" + filter;

            query += $"&$top={maxResults}";

            if (skip > 0)
                query += $"&$skip={skip}";

            query += "&$count=true";

            return query;
        }

        private static string EscapeODataString(string value)
        {
            return Uri.EscapeDataString(value ?? "").Replace("'", "''");
        }

        private static (IReadOnlyList<PartSearchResult> Items, int TotalCount) ParseResponse(string json)
        {
            var results = new List<PartSearchResult>();
            var totalCount = 0;

            var root = JObject.Parse(json);
            var items = root["value"] as JArray;
            if (items == null)
                return (results, totalCount);

            // Parse @odata.count for pagination
            var countToken = root["@odata.count"];
            if (countToken != null)
                int.TryParse(countToken.ToString(), out totalCount);

            foreach (var item in items)
            {
                var partSummary = new PartSummary
                {
                    Id = GetString(item, "id"),
                    PartNumber = GetString(item, "item_number"),
                    Name = GetString(item, "name"),
                    Description = GetString(item, "description"),
                    Revision = GetString(item, "major_rev"),
                    State = GetString(item, "state"),
                    PartType = GetString(item, "classification")
                };

                var ironCad = ReadIronCadPartCad(item);

                results.Add(new PartSearchResult
                {
                    Part = partSummary,
                    IronCadPartCad = ironCad
                });
            }

            return (results, totalCount);
        }

        internal static CadSummary ReadIronCadPartCad(JToken partEntry)
        {
            var cadRels = partEntry["Part_CAD"] as JArray;
            if (cadRels == null)
                return null;

            foreach (var rel in cadRels)
            {
                var cadEntry = rel["related_id"];
                if (cadEntry == null)
                    continue;

                var authoringTool = GetString(cadEntry, "authoring_tool");
                var nativeFile = GetString(cadEntry, "native_file");

                if (!CadResolutionHelper.IsIronCadWithValidNativeFile(authoringTool, nativeFile))
                    continue;

                var lockedById = GetString(cadEntry, "locked_by_id");

                return new CadSummary
                {
                    Id = GetString(cadEntry, "id"),
                    CadNumber = GetString(cadEntry, "item_number"),
                    Classification = GetString(cadEntry, "classification"),
                    Revision = GetString(cadEntry, "major_rev"),
                    State = GetString(cadEntry, "state"),
                    Generation = GetInt(cadEntry, "generation"),
                    NativeFileId = nativeFile,
                    HasNativeFile = true,
                    IsLocked = !string.IsNullOrWhiteSpace(lockedById),
                    LockedBy = lockedById
                };
            }

            return null;
        }

        private static string GetString(JToken el, string property)
        {
            var prop = el[property];
            return prop?.Type == JTokenType.String ? prop.Value<string>() : null;
        }

        private static int GetInt(JToken el, string property)
        {
            var prop = el[property];
            if (prop == null) return 0;
            if (prop.Type == JTokenType.Integer) return prop.Value<int>();
            if (prop.Type == JTokenType.String && int.TryParse(prop.Value<string>(), out var val)) return val;
            return 0;
        }
    }
}
