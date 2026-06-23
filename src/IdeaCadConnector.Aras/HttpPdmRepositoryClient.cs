using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging;

namespace IdeaCadConnector.Aras
{
    public sealed class HttpPdmRepositoryClient : IPdmRepositoryClient, IDisposable
    {
        private readonly ArasClientOptions _options;
        private readonly ILogger<HttpPdmRepositoryClient> _logger;
        private ArasHttpClient _http;
        private ArasAmlClient _aml;
        private bool _disposed;

        public HttpPdmRepositoryClient(ArasClientOptions options, ILogger<HttpPdmRepositoryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpPdmRepositoryClient>.Instance;
        }

        public void SetSession(string accessToken, string tokenType, string database)
        {
            if (_http == null)
            {
                _http = new ArasHttpClient(_options.BaseUri, _options.Timeout);
            }
            _http.SetBearerToken(accessToken, tokenType ?? "Bearer");
            _aml = new ArasAmlClient(_http, database ?? _options.Database);
        }

        public async Task<PdmPushResult> PushAsync(PdmPushRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            EnsureAuthenticated();

            var result = new PdmPushResult();
            var partResults = new List<PdmItemResult>();
            var cadResults = new List<PdmItemResult>();
            var docResults = new List<PdmItemResult>();

            try
            {
                var partIdByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string projectId = null;

                foreach (var part in request.Parts ?? Array.Empty<PdmPartRequest>())
                {
                    var id = await CreateOrGetPartAsync(part, ct);
                    partResults.Add(id);
                    if (id.Success && !string.IsNullOrWhiteSpace(id.ArasId))
                        partIdByCode[part.LogicalCode] = id.ArasId;
                }

                foreach (var part in request.Parts ?? Array.Empty<PdmPartRequest>())
                {
                    if (!string.IsNullOrWhiteSpace(part.ParentLogicalCode) &&
                        partIdByCode.TryGetValue(part.LogicalCode, out var childId) &&
                        partIdByCode.TryGetValue(part.ParentLogicalCode, out var parentId))
                    {
                        await EnsurePartBomAsync(parentId, childId, part.Quantity, ct);
                    }
                }

                foreach (var cad in request.Cads ?? Array.Empty<PdmCadRequest>())
                {
                    var linkedPartId = !string.IsNullOrWhiteSpace(cad.LinkedPartLogicalCode) &&
                        partIdByCode.TryGetValue(cad.LinkedPartLogicalCode, out var pid) ? pid : null;

                    var id = await CreateOrGetCadAsync(cad, linkedPartId, ct);
                    cadResults.Add(id);
                }

                foreach (var doc in request.Documents ?? Array.Empty<PdmDocumentRequest>())
                {
                    var isProjectLevel = string.Equals(doc.LinkTargetType, "Project", StringComparison.OrdinalIgnoreCase);
                    string linkedPartId = null;
                    if (!isProjectLevel &&
                        !string.IsNullOrWhiteSpace(doc.LinkedPartLogicalCode) &&
                        partIdByCode.TryGetValue(doc.LinkedPartLogicalCode, out var pid))
                    {
                        linkedPartId = pid;
                    }

                    if (isProjectLevel && string.IsNullOrWhiteSpace(projectId))
                    {
                        projectId = await CreateOrGetProjectAsync(request.RepositoryCode, request.ProjectName, ct);
                    }

                    var id = await CreateOrGetDocumentAsync(doc, linkedPartId, projectId, ct);
                    docResults.Add(id);
                }

                var allBusinessSuccess = partResults.All(r => r.Success) &&
                    cadResults.All(r => r.Success) &&
                    docResults.All(r => r.Success);

                if (allBusinessSuccess)
                {
                    result.Success = true;

                    try
                    {
                        var commitId = await CreatePdmCommitAsync(request, partResults, cadResults, docResults, ct);
                        result.CommitId = commitId;
                    }
                    catch (Exception ex)
                    {
                        var warnings = new List<string>(result.Warnings ?? Array.Empty<string>())
                        {
                            "Commit snapshot skipped because PDM Commit ItemType is not deployed on server: " + ex.Message
                        };
                        result.Warnings = warnings;
                        _logger.LogWarning(ex, "PDM Commit schema unavailable. Business push completed without commit snapshot.");
                    }
                }
                else
                {
                    result.Success = false;
                    var failedParts = partResults.Where(r => !r.Success).Select(r => $"  - {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}");
                    var failedCads = cadResults.Where(r => !r.Success).Select(r => $"  - {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}");
                    var failedDocs = docResults.Where(r => !r.Success).Select(r => $"  - {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}");
                    result.ErrorMessage = $"Business item(s) failed:{Environment.NewLine}Parts:{Environment.NewLine}{string.Join(Environment.NewLine, failedParts)}{Environment.NewLine}CADs:{Environment.NewLine}{string.Join(Environment.NewLine, failedCads)}{Environment.NewLine}Documents:{Environment.NewLine}{string.Join(Environment.NewLine, failedDocs)}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDM push failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            result.PartResults = partResults;
            result.CadResults = cadResults;
            result.DocumentResults = docResults;
            return result;
        }

        private async Task<PdmItemResult> CreateOrGetPartAsync(PdmPartRequest part, CancellationToken ct)
        {
            try
            {
                var existingId = await FindItemByNumberAsync("Part", part.PartNumber, ct);
                if (existingId != null)
                {
                    return new PdmItemResult
                    {
                        SourceKey = part.LogicalCode,
                        ArasId = existingId,
                        ItemNumber = part.PartNumber,
                        Success = true
                    };
                }

                var aml = $"<Item type=\"Part\" action=\"add\">" +
                    $"<item_number>{EscapeAml(part.PartNumber)}</item_number>" +
                    $"<name>{EscapeAml(part.Name ?? part.LogicalCode)}</name>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "add", "Part", null, ct);
                var newId = response?["id"]?.ToString();

                return new PdmItemResult
                {
                    SourceKey = part.LogicalCode,
                    ArasId = newId,
                    ItemNumber = part.PartNumber,
                    Success = !string.IsNullOrWhiteSpace(newId),
                    ErrorMessage = newId == null
                        ? $"Part add failed. number='{part.PartNumber}', classification='{part.Classification} (preview-only, not sent to Aras)', name='{part.Name ?? part.LogicalCode}'. Aras returned no id."
                        : null
                };
            }
            catch (Exception ex)
            {
                return new PdmItemResult
                {
                    SourceKey = part.LogicalCode,
                    ItemNumber = part.PartNumber,
                    Success = false,
                    ErrorMessage = $"Part add failed. number='{part.PartNumber}', classification='{part.Classification} (preview-only, not sent to Aras)', name='{part.Name ?? part.LogicalCode}'. Aras said: {ex.Message}"
                };
            }
        }

        private async Task<PdmItemResult> CreateOrGetCadAsync(PdmCadRequest cad, string linkedPartId, CancellationToken ct)
        {
            try
            {
                var existingId = await FindItemByNumberAsync("CAD", cad.CadNumber, ct);
                if (existingId != null)
                {
                    if (!string.IsNullOrWhiteSpace(linkedPartId))
                    {
                        await EnsureRelationshipAsync("Part CAD", linkedPartId, existingId, ct);
                    }

                    return new PdmItemResult
                    {
                        SourceKey = cad.SourceFileName,
                        ArasId = existingId,
                        ItemNumber = cad.CadNumber,
                        Success = true
                    };
                }

                var aml = $"<Item type=\"CAD\" action=\"add\">" +
                    $"<item_number>{EscapeAml(cad.CadNumber)}</item_number>" +
                    $"<classification>{EscapeAml(cad.Classification)}</classification>" +
                    "<name>" + EscapeAml(cad.SourceFileName) + "</name>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "add", "CAD", null, ct);
                var newId = response?["id"]?.ToString();

                if (!string.IsNullOrWhiteSpace(newId) && !string.IsNullOrWhiteSpace(linkedPartId))
                {
                    await EnsureRelationshipAsync("Part CAD", linkedPartId, newId, ct);
                }

                return new PdmItemResult
                {
                    SourceKey = cad.SourceFileName,
                    ArasId = newId,
                    ItemNumber = cad.CadNumber,
                    Success = !string.IsNullOrWhiteSpace(newId),
                    ErrorMessage = newId == null ? "No id returned from Aras" : null
                };
            }
            catch (Exception ex)
            {
                return new PdmItemResult
                {
                    SourceKey = cad.SourceFileName,
                    ItemNumber = cad.CadNumber,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<PdmItemResult> CreateOrGetDocumentAsync(PdmDocumentRequest doc, string linkedPartId, string projectId, CancellationToken ct)
        {
            try
            {
                var existingId = await FindItemByNumberAsync("Document", doc.DocumentNumber, ct);
                var isProjectLevel = string.Equals(doc.LinkTargetType, "Project", StringComparison.OrdinalIgnoreCase);
                var relationType = isProjectLevel ? "Project Document" : "Part Document";
                var sourceId = isProjectLevel ? projectId : linkedPartId;

                if (existingId != null)
                {
                    if (!string.IsNullOrWhiteSpace(sourceId))
                    {
                        await EnsureRelationshipAsync(relationType, sourceId, existingId, ct);
                    }

                    return new PdmItemResult
                    {
                        SourceKey = doc.SourceFileName,
                        ArasId = existingId,
                        ItemNumber = doc.DocumentNumber,
                        Success = true
                    };
                }

                var aml = $"<Item type=\"Document\" action=\"add\">" +
                    $"<item_number>{EscapeAml(doc.DocumentNumber)}</item_number>" +
                    $"<name>{EscapeAml(doc.SourceFileName)}</name>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "add", "Document", null, ct);
                var newId = response?["id"]?.ToString();

                if (!string.IsNullOrWhiteSpace(newId) && !string.IsNullOrWhiteSpace(sourceId))
                {
                    await EnsureRelationshipAsync(relationType, sourceId, newId, ct);
                }

                return new PdmItemResult
                {
                    SourceKey = doc.SourceFileName,
                    ArasId = newId,
                    ItemNumber = doc.DocumentNumber,
                    Success = !string.IsNullOrWhiteSpace(newId),
                    ErrorMessage = newId == null
                        ? $"Document add failed. number='{doc.DocumentNumber}', classification='{doc.Classification} (preview-only, not sent to Aras)', source='{doc.SourceFileName}'. Aras returned no id."
                        : null
                };
            }
            catch (Exception ex)
            {
                return new PdmItemResult
                {
                    SourceKey = doc.SourceFileName,
                    ItemNumber = doc.DocumentNumber,
                    Success = false,
                    ErrorMessage = $"Document add failed. number='{doc.DocumentNumber}', classification='{doc.Classification} (preview-only, not sent to Aras)', source='{doc.SourceFileName}'. Aras said: {ex.Message}"
                };
            }
        }

        private async Task EnsurePartBomAsync(string parentId, string childId, int quantity, CancellationToken ct)
        {
            var existingRelId = await FindRelationshipAsync("Part BOM", parentId, childId, ct);
            if (!string.IsNullOrWhiteSpace(existingRelId))
            {
                return;
            }

            var relId = Guid.NewGuid().ToString("N").ToUpperInvariant();
            var aml = $"<Item type=\"Part BOM\" action=\"add\" id=\"{relId}\">" +
                $"<source_id>{EscapeAml(parentId)}</source_id>" +
                $"<related_id>{EscapeAml(childId)}</related_id>" +
                $"<quantity>{quantity}</quantity>" +
                "</Item>";

            await _aml.ApplyAmlAsync(aml, "add", "Part BOM", null, ct).ConfigureAwait(false);
        }

        private async Task EnsureRelationshipAsync(string relType, string sourceId, string relatedId, CancellationToken ct)
        {
            var existingRelId = await FindRelationshipAsync(relType, sourceId, relatedId, ct);
            if (!string.IsNullOrWhiteSpace(existingRelId))
            {
                return;
            }

            var relId = Guid.NewGuid().ToString("N").ToUpperInvariant();
            var aml = $"<Item type=\"{EscapeAml(relType)}\" action=\"add\" id=\"{relId}\">" +
                $"<source_id>{EscapeAml(sourceId)}</source_id>" +
                $"<related_id>{EscapeAml(relatedId)}</related_id>" +
                "</Item>";

            await _aml.ApplyAmlAsync(aml, "add", relType, null, ct).ConfigureAwait(false);
        }

        private async Task<string> CreatePdmCommitAsync(
            PdmPushRequest request,
            List<PdmItemResult> parts,
            List<PdmItemResult> cads,
            List<PdmItemResult> docs,
            CancellationToken ct)
        {
            var commitId = Guid.NewGuid().ToString("N").ToUpperInvariant();
            var aml = $"<Item type=\"PDM Commit\" action=\"add\" id=\"{commitId}\">" +
                $"<commit_code>{EscapeAml(commitId.Substring(0, 8))}</commit_code>" +
                $"<repository_code>{EscapeAml(request.RepositoryCode)}</repository_code>" +
                $"<branch_name>{EscapeAml(request.TargetBranch ?? "main")}</branch_name>" +
                $"<message>{EscapeAml(request.CommitMessage ?? "")}</message>" +
                $"<package_source_path>{EscapeAml(request.PackageSourcePath ?? "")}</package_source_path>" +
                $"<cad_source_path>{EscapeAml(request.CadSourcePath ?? "")}</cad_source_path>" +
                "</Item>";

            var response = await _aml.ApplyAmlAsync(aml, "add", "PDM Commit", null, ct);
            var returnedId = response?["id"]?.ToString() ?? commitId;

            foreach (var pr in parts.Where(p => p.Success))
            {
                await CreateCommitFileEntryAsync(returnedId, pr.ItemNumber, "part", ct);
            }

            foreach (var cad in cads.Where(c => c.Success))
            {
                await CreateCommitFileEntryAsync(returnedId, cad.ItemNumber, "cad", ct);
            }

            foreach (var doc in docs.Where(d => d.Success))
            {
                await CreateCommitFileEntryAsync(returnedId, doc.ItemNumber, "document", ct);
            }

            return returnedId;
        }

        private async Task<string> CreateOrGetProjectAsync(string projectNumber, string projectName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(projectNumber))
            {
                return null;
            }

            var existingId = await FindItemByNumberAsync("Project", projectNumber, ct);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }

            var aml = $"<Item type=\"Project\" action=\"add\">" +
                $"<item_number>{EscapeAml(projectNumber)}</item_number>" +
                $"<name>{EscapeAml(string.IsNullOrWhiteSpace(projectName) ? projectNumber : projectName)}</name>" +
                "</Item>";

            var response = await _aml.ApplyAmlAsync(aml, "add", "Project", null, ct);
            return response?["id"]?.ToString();
        }

        private async Task<string> FindRelationshipAsync(string relType, string sourceId, string relatedId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(relType) ||
                string.IsNullOrWhiteSpace(sourceId) ||
                string.IsNullOrWhiteSpace(relatedId))
            {
                return null;
            }

            try
            {
                var aml = $"<Item type=\"{EscapeAml(relType)}\" action=\"get\" select=\"id\">" +
                    $"<source_id>{EscapeAml(sourceId)}</source_id>" +
                    $"<related_id>{EscapeAml(relatedId)}</related_id>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "get", relType, null, ct);
                var items = response?["Items"];
                if (items != null && items.HasValues)
                {
                    return items[0]?["id"]?.ToString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task CreateCommitFileEntryAsync(string commitId, string relativePath, string fileRole, CancellationToken ct)
        {
            var fileAml = $"<Item type=\"PDM Commit File\" action=\"add\">" +
                $"<commit_id>{EscapeAml(commitId)}</commit_id>" +
                $"<relative_path>{EscapeAml(relativePath)}</relative_path>" +
                $"<file_role>{EscapeAml(fileRole)}</file_role>" +
                $"<change_type>added</change_type>" +
                "</Item>";

            await _aml.ApplyAmlAsync(fileAml, "add", "PDM Commit File", null, ct);
        }

        private async Task<string> FindItemByNumberAsync(string itemType, string itemNumber, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(itemNumber))
                return null;

            try
            {
                var aml = $"<Item type=\"{EscapeAml(itemType)}\" action=\"get\" select=\"id\">" +
                    $"<item_number>{EscapeAml(itemNumber)}</item_number>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "get", itemType, null, ct);
                var items = response?["Items"];
                if (items != null && items.HasValues)
                    return items[0]?["id"]?.ToString();

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void EnsureAuthenticated()
        {
            if (_http == null || _aml == null)
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "HttpPdmRepositoryClient is not authenticated. Call SetSession() after login.");
        }

        private static string EscapeAml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http?.Dispose();
                _disposed = true;
            }
        }
    }
}
