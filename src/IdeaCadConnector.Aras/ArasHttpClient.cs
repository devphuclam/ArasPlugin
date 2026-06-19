using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Low-level HTTP helper for Aras Innovator REST API.
    /// Centralizes base address, OAuth bearer token, JSON handling, and error mapping.
    /// </summary>
    internal sealed class ArasHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public ArasHttpClient(Uri baseUri, TimeSpan timeout)
        {
            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            _httpClient = new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = timeout
            };
        }

        public void SetBearerToken(string token, string tokenType = "Bearer")
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(tokenType ?? "Bearer", token);
        }

        public async Task<JObject> GetJsonAsync(string requestUri, CancellationToken ct)
        {
            using var response = await _httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, body);
            return string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
        }

        public async Task<JObject> PostJsonAsync(string requestUri, object body, CancellationToken ct)
        {
            using var content = CreateJsonContent(body);
            using var response = await _httpClient.PostAsync(requestUri, content, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return string.IsNullOrWhiteSpace(responseBody) ? new JObject() : JObject.Parse(responseBody);
        }

        public async Task<JObject> PostJsonAsync(string requestUri, string body, CancellationToken ct)
        {
            using var content = new StringContent(body ?? "", Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(requestUri, content, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return string.IsNullOrWhiteSpace(responseBody) ? new JObject() : JObject.Parse(responseBody);
        }

        public async Task<JObject> PatchJsonAsync(string requestUri, object body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri)
            {
                Content = CreateJsonContent(body)
            };
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return string.IsNullOrWhiteSpace(responseBody) ? new JObject() : JObject.Parse(responseBody);
        }

        public async Task<JObject> PostMultipartAsync(string requestUri, HttpContent content, CancellationToken ct)
        {
            using var response = await _httpClient.PostAsync(requestUri, content, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return string.IsNullOrWhiteSpace(responseBody) ? new JObject() : JObject.Parse(responseBody);
        }

        public async Task<string> PostXmlAsync(string requestUri, HttpContent content, CancellationToken ct)
        {
            using var response = await _httpClient.PostAsync(requestUri, content, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return responseBody ?? string.Empty;
        }

        public async Task<string> PostXmlAsync(
            string requestUri,
            HttpContent content,
            IDictionary<string, string> headers,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            };

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    request.Headers.Add(pair.Key, pair.Value);
                }
            }

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return responseBody ?? string.Empty;
        }

        public async Task<string> PostRawAsync(
            string requestUri,
            HttpContent content,
            IDictionary<string, string> headers,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content
            };

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    request.Headers.Add(pair.Key, pair.Value);
                }
            }

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CheckError(response, responseBody);
            return responseBody ?? string.Empty;
        }

        public async Task<byte[]> GetBytesAsync(string requestUri, CancellationToken ct)
        {
            using var response = await _httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                CheckError(response, body);
            }
            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        private static StringContent CreateJsonContent(object body)
        {
            var json = body is string s ? s : JsonConvert.SerializeObject(body);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static void CheckError(HttpResponseMessage response, string body)
        {
            if (response.IsSuccessStatusCode)
                return;

            var code = (int)response.StatusCode switch
            {
                400 => ArasErrorCode.ValidationFailed,
                401 => ArasErrorCode.AuthExpired,
                403 => ArasErrorCode.PermissionDenied,
                404 => ArasErrorCode.CadNotFound,
                >= 500 => ArasErrorCode.ServerUnavailable,
                _ => ArasErrorCode.UnexpectedServerError
            };

            string message = $"Aras HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var errorJson = JObject.Parse(body);
                    var errorMessage = errorJson["error"]?["message"]?.Value<string>()
                        ?? errorJson["message"]?.Value<string>()
                        ?? body;
                    message += " " + errorMessage;
                }
                catch
                {
                    message += " " + body;
                }
            }

            throw new ArasOperationException(
                code,
                message,
                retryable: (int)response.StatusCode >= 500,
                details: new Dictionary<string, string> { ["statusCode"] = ((int)response.StatusCode).ToString() });
        }
    }
}
