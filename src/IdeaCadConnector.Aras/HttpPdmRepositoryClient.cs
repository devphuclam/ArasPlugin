using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    public sealed class HttpPdmRepositoryClient : IPdmRepositoryClient, IDisposable
    {
        private const string IronCadAssemblyClassification = "Mechanical/Assembly";
        private readonly ArasClientOptions _options;
        private readonly ILogger<HttpPdmRepositoryClient> _logger;
        private ArasHttpClient _http;
        private IArasAmlClient _aml;
        private VaultClient _vault;
        private bool _disposed;

        public HttpPdmRepositoryClient(ArasClientOptions options, ILogger<HttpPdmRepositoryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpPdmRepositoryClient>.Instance;
        }

        internal HttpPdmRepositoryClient(
            ArasClientOptions options,
            IArasAmlClient amlClient,
            ILogger<HttpPdmRepositoryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _aml = amlClient ?? throw new ArgumentNullException(nameof(amlClient));
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
            _vault = new VaultClient(_http, _options);
        }

        public async Task<PdmExistencePreview> PreviewExistenceAsync(PdmPushRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            EnsureAuthenticated();

            var partsByNumber = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var cadsByNumber = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var docsByNumber = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var bomByChildLogicalCode = new Dictionary<string, PdmBomExistenceInfo>(StringComparer.OrdinalIgnoreCase);
            var partIdsByLogicalCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var partNumberByLogicalCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in request.Parts ?? Array.Empty<PdmPartRequest>())
            {
                var number = part.PartNumber;
                var id = !string.IsNullOrWhiteSpace(part.ExistingPartId)
                    ? (await GetPartByIdAsync(part.ExistingPartId, ct).ConfigureAwait(false))?.Id
                    : await FindItemByNumberAsync("Part", number, ct).ConfigureAwait(false);
                partsByNumber[number] = id != null;
                partNumberByLogicalCode[part.LogicalCode] = number;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    partIdsByLogicalCode[part.LogicalCode] = id;
                }
            }

            foreach (var cad in request.Cads ?? Array.Empty<PdmCadRequest>())
            {
                var number = cad.CadNumber;
                var id = await FindItemByNumberAsync("CAD", number, ct).ConfigureAwait(false);
                cadsByNumber[number] = id != null;
            }

            foreach (var doc in request.Documents ?? Array.Empty<PdmDocumentRequest>())
            {
                var number = doc.DocumentNumber;
                var id = await FindItemByNumberAsync("Document", number, ct).ConfigureAwait(false);
                docsByNumber[number] = id != null;
            }

            foreach (var part in request.Parts ?? Array.Empty<PdmPartRequest>())
            {
                if (string.IsNullOrWhiteSpace(part.ParentLogicalCode))
                {
                    continue;
                }

                var info = new PdmBomExistenceInfo();
                if (partIdsByLogicalCode.TryGetValue(part.ParentLogicalCode, out var parentId) &&
                    partIdsByLogicalCode.TryGetValue(part.LogicalCode, out var childId))
                {
                    info = await FindPartBomInfoAsync(parentId, childId, ct).ConfigureAwait(false);
                }
                else if (partNumberByLogicalCode.ContainsKey(part.ParentLogicalCode) &&
                    partNumberByLogicalCode.ContainsKey(part.LogicalCode))
                {
                    info = new PdmBomExistenceInfo
                    {
                        Exists = false
                    };
                }

                bomByChildLogicalCode[part.LogicalCode] = info;
            }

            return new PdmExistencePreview
            {
                PartsByNumber = partsByNumber,
                CadsByNumber = cadsByNumber,
                DocumentsByNumber = docsByNumber,
                BomByChildLogicalCode = bomByChildLogicalCode
            };
        }

        public async Task<string> FindItemIdByNumberAsync(string itemType, string itemNumber, CancellationToken ct)
        {
            EnsureAuthenticated();
            return await FindItemByNumberAsync(itemType, itemNumber, ct).ConfigureAwait(false);
        }

        public async Task<PdmCloneResult> CloneLatestToWorkspaceAsync(PdmCloneRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.RepositoryCode))
                throw new ArgumentException("RepositoryCode is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.TargetFolder))
                throw new ArgumentException("TargetFolder is required.", nameof(request));

            EnsureAuthenticated();

            var warnings = new List<string>();
            var repositoryCode = request.RepositoryCode.Trim();
            var projectFolder = request.TargetFolder.Trim();
            var cadFolder = projectFolder;
            var drawingsFolder = Path.Combine(projectFolder, "ARAS01");

            Directory.CreateDirectory(projectFolder);
            Directory.CreateDirectory(drawingsFolder);

            var rootPart = await GetPartByNumberAsync(repositoryCode, ct).ConfigureAwait(false);
            if (rootPart == null)
            {
                return new PdmCloneResult
                {
                    Success = false,
                    RepositoryCode = repositoryCode,
                    ResolvedProjectFolder = projectFolder,
                    ResolvedCadFolder = cadFolder,
                    ErrorMessage = "Root Part not found on Aras for repository '" + repositoryCode + "'."
                };
            }

            if (!string.IsNullOrWhiteSpace(request.BranchName) &&
                !string.Equals(request.BranchName, "main", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Clone currently uses latest live data on Aras. Branch '" + request.BranchName + "' is local-only and was not resolved on server.");
            }

            var partQueue = new Queue<ClonePartInfo>();
            var partIdsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cadFileIdsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var documentNamesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clonedParts = new Dictionary<string, ClonePartInfo>(StringComparer.OrdinalIgnoreCase);
            var selectedCadByPartId = new Dictionary<string, CloneCadInfo>(StringComparer.OrdinalIgnoreCase);
            var downloadedCadCount = 0;
            var placeholderDocumentCount = 0;

            partQueue.Enqueue(rootPart);
            partIdsSeen.Add(rootPart.Id);
            clonedParts[rootPart.Id] = rootPart;

            while (partQueue.Count > 0)
            {
                var part = partQueue.Dequeue();
                var cadExpected = IsCadExpectedForPart(part.ItemNumber, string.Equals(part.Id, rootPart.Id, StringComparison.OrdinalIgnoreCase));

                var cadCandidates = await GetPartCadCandidatesAsync(part.Id, ct).ConfigureAwait(false);
                var selectedCad = SelectPreferredCad(cadCandidates, part.ItemNumber, string.Equals(part.Id, rootPart.Id, StringComparison.OrdinalIgnoreCase));
                if (selectedCad == null && cadExpected)
                {
                    selectedCad = await FindFallbackCadAsync(part.ItemNumber, string.Equals(part.Id, rootPart.Id, StringComparison.OrdinalIgnoreCase), ct).ConfigureAwait(false);
                }
                if (selectedCad != null)
                {
                    selectedCadByPartId[part.Id] = selectedCad;
                    if (string.IsNullOrWhiteSpace(selectedCad.NativeFileId))
                    {
                        warnings.Add("CAD '" + selectedCad.ItemNumber + "' exists but has no native file on Aras.");
                    }
                    else if (cadFileIdsSeen.Add(selectedCad.NativeFileId))
                    {
                        await _vault.DownloadFileAsync(selectedCad.NativeFileId, cadFolder, ct).ConfigureAwait(false);
                        downloadedCadCount++;
                    }
                }
                else if (cadExpected)
                {
                    warnings.Add("No usable IronCAD record found for Part '" + part.ItemNumber + "'.");
                }

                var partDocumentNames = await GetRelatedDocumentNamesAsync("Part Document", part.Id, ct).ConfigureAwait(false);
                foreach (var documentName in partDocumentNames)
                {
                    if (documentNamesSeen.Add(documentName))
                    {
                        var targetPath = Path.Combine(projectFolder, documentName);
                        EnsurePlaceholderFile(targetPath);
                        placeholderDocumentCount++;
                    }
                }

                var childIds = await GetChildPartIdsAsync(part.Id, ct).ConfigureAwait(false);
                foreach (var childId in childIds)
                {
                    if (!partIdsSeen.Add(childId))
                        continue;

                    var childPart = await GetPartByIdAsync(childId, ct).ConfigureAwait(false);
                    if (childPart == null)
                    {
                        warnings.Add("Child Part id '" + childId + "' could not be loaded during clone.");
                        continue;
                    }

                    childPart.ParentId = part.Id;
                    clonedParts[childPart.Id] = childPart;
                    partQueue.Enqueue(childPart);
                }
            }

            var projectId = await FindItemByNumberAsync("Project", repositoryCode, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                var projectDocumentNames = await GetRelatedDocumentNamesAsync("Project Document", projectId, ct).ConfigureAwait(false);
                foreach (var documentName in projectDocumentNames)
                {
                    if (documentNamesSeen.Add(documentName))
                    {
                        var targetPath = Path.Combine(projectFolder, documentName);
                        EnsurePlaceholderFile(targetPath);
                        placeholderDocumentCount++;
                    }
                }
            }

            placeholderDocumentCount += GeneratePackageShapeFiles(
                repositoryCode,
                projectFolder,
                drawingsFolder,
                rootPart,
                clonedParts.Values,
                selectedCadByPartId);

            return new PdmCloneResult
            {
                Success = downloadedCadCount > 0 || placeholderDocumentCount > 0,
                RepositoryCode = repositoryCode,
                RootPartId = rootPart.Id,
                RootPartNumber = rootPart.ItemNumber,
                ResolvedProjectFolder = projectFolder,
                ResolvedCadFolder = cadFolder,
                DownloadedCadFileCount = downloadedCadCount,
                PlaceholderDocumentCount = placeholderDocumentCount,
                Warnings = warnings,
                ErrorMessage = downloadedCadCount == 0 && placeholderDocumentCount == 0
                    ? "No CAD native files or related document placeholders could be cloned from Aras."
                    : null
            };
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

            var isMainBranch = string.Equals(request.TargetBranch, "main", StringComparison.OrdinalIgnoreCase);

            if (!isMainBranch)
            {
                var stagingMsg = $"Non-main branch '{request.TargetBranch}': push created staging snapshot only. Live Part/BOM/CAD/Document data was not updated.";
                _logger.LogInformation(stagingMsg);

                try
                {
                    var commitId = await CreatePdmCommitAsync(request, partResults, cadResults, docResults, ct);
                    result.CommitId = commitId;
                    result.Success = !string.IsNullOrWhiteSpace(commitId);
                    result.StagingOnly = true;
                    result.LiveDataUpdated = false;
                    result.Warnings = new[] { stagingMsg };
                    if (!result.Success)
                    {
                        result.ErrorMessage = $"Non-main branch '{request.TargetBranch}' is blocked from live push, and staging snapshot did not return a commit id.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PDM Commit schema unavailable. Staging snapshot skipped.");
                    result.Success = false;
                    result.StagingOnly = false;
                    result.LiveDataUpdated = false;
                    result.ErrorMessage = $"Non-main branch '{request.TargetBranch}' is blocked from live push, and staging snapshot could not be created.";
                    result.Warnings = new[] { stagingMsg, "PDM Commit schema unavailable. Staging snapshot skipped." };
                }
                result.PartResults = partResults;
                result.CadResults = cadResults;
                result.DocumentResults = docResults;
                return result;
            }

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

                var bomFailures = new List<string>();
                var bomResults = new List<PdmBomPushResult>();
                foreach (var part in request.Parts ?? Array.Empty<PdmPartRequest>())
                {
                    if (!string.IsNullOrWhiteSpace(part.ParentLogicalCode) &&
                        partIdByCode.TryGetValue(part.LogicalCode, out var childId) &&
                        partIdByCode.TryGetValue(part.ParentLogicalCode, out var parentId))
                    {
                        try
                        {
                            var bomResult = await EnsurePartBomAsync(parentId, childId, part.Quantity, ct).ConfigureAwait(false);
                            var bomSuccess = bomResult == BomActionResult.Created ||
                                             bomResult == BomActionResult.QuantityUpdated ||
                                             bomResult == BomActionResult.Unchanged;
                            var bomPushResult = new PdmBomPushResult
                            {
                                ParentLogicalCode = part.ParentLogicalCode,
                                ChildLogicalCode = part.LogicalCode,
                                ParentPartId = parentId,
                                ChildPartId = childId,
                                Quantity = part.Quantity,
                                Success = bomSuccess,
                                ActionTaken = bomResult
                            };
                            if (bomResult == BomActionResult.InvalidParentChild)
                            {
                                var msg = $"Part {part.PartNumber ?? part.LogicalCode}: parent and child resolve to the same Aras Part ID ({parentId}).";
                                _logger.LogWarning("BOM blocked: {Msg}", msg);
                                bomPushResult.ErrorMessage = msg;
                                bomFailures.Add(msg);
                            }
                            else if (bomResult == BomActionResult.InvalidQuantity)
                            {
                                var msg = $"Part {part.PartNumber ?? part.LogicalCode}: quantity must be greater than zero (got {part.Quantity}).";
                                _logger.LogWarning("BOM blocked: {Msg}", msg);
                                bomPushResult.ErrorMessage = msg;
                                bomFailures.Add(msg);
                            }
                            bomResults.Add(bomPushResult);
                        }
                        catch (ArasOperationException ex)
                        {
                            var msg = ClassifyArasError(ex) ?? "BOM relationship failed: " + SanitizeForUser(ex.Message);
                            _logger.LogWarning(ex, "BOM upsert failed for Part {PartNumber} (parent={ParentId} child={ChildId}): {Msg}", part.PartNumber, parentId, childId, msg);
                            bomFailures.Add($"{part.PartNumber ?? part.LogicalCode}: {msg}");
                            bomResults.Add(new PdmBomPushResult
                            {
                                ParentLogicalCode = part.ParentLogicalCode,
                                ChildLogicalCode = part.LogicalCode,
                                ParentPartId = parentId,
                                ChildPartId = childId,
                                Quantity = part.Quantity,
                                Success = false,
                                ActionTaken = BomActionResult.Failed,
                                ErrorMessage = msg
                            });
                        }
                        catch (Exception ex) when (
                            ex is System.Net.Http.HttpRequestException ||
                            ex is System.Threading.Tasks.TaskCanceledException ||
                            ex is System.TimeoutException ||
                            ex is System.IO.IOException)
                        {
                            var msg = "BOM relationship failed due to a network or timeout error.";
                            _logger.LogWarning(ex, "BOM network failure for Part {PartNumber} (parent={ParentId} child={ChildId})", part.PartNumber, parentId, childId);
                            bomFailures.Add($"{part.PartNumber ?? part.LogicalCode}: {msg}");
                            bomResults.Add(new PdmBomPushResult
                            {
                                ParentLogicalCode = part.ParentLogicalCode,
                                ChildLogicalCode = part.LogicalCode,
                                ParentPartId = parentId,
                                ChildPartId = childId,
                                Quantity = part.Quantity,
                                Success = false,
                                ActionTaken = BomActionResult.Failed,
                                ErrorMessage = msg
                            });
                        }
                    }
                }

                foreach (var cad in request.Cads ?? Array.Empty<PdmCadRequest>())
                {
                    var linkedPartRequest = request.Parts?.FirstOrDefault(p =>
                        string.Equals(p.LogicalCode, cad.LinkedPartLogicalCode, StringComparison.OrdinalIgnoreCase));

                    if (linkedPartRequest != null &&
                        (linkedPartRequest.IsExternalReference ||
                         string.Equals(linkedPartRequest.SourceKind, "LibraryReference", StringComparison.OrdinalIgnoreCase)))
                    {
                        cadResults.Add(new PdmItemResult
                        {
                            SourceKey = cad.SourceFileName,
                            ItemNumber = cad.CadNumber,
                            Success = true,
                            ActionTaken = "SkippedLibraryReference"
                        });
                        continue;
                    }

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

                var partsSucceeded = partResults.All(r => r.Success);
                var docsSucceeded = docResults.All(r => r.Success);
                var cadsMetadataSucceeded = cadResults.All(r => r.Success);
                var hasBomFailure = bomFailures.Count > 0;
                var allBusinessSuccess = partsSucceeded && docsSucceeded && !hasBomFailure;

                if (allBusinessSuccess)
                {
                    result.Success = true;
                    result.LiveDataUpdated = true;
                    result.StagingOnly = false;

                    var warnings = new List<string>(result.Warnings ?? Array.Empty<string>());
                    if (!cadsMetadataSucceeded)
                    {
                        var fileFailures = cadResults
                            .Where(r => !r.Success)
                            .Select(r => $"CAD '{r.SourceKey}': {r.ErrorMessage ?? "native file attach failed"}")
                            .ToList();
                        warnings.AddRange(fileFailures);
                    }

                    try
                    {
                        var commitId = await CreatePdmCommitAsync(request, partResults, cadResults, docResults, ct);
                        result.CommitId = commitId;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add("Commit snapshot skipped because PDM Commit ItemType is not deployed on server: " + ex.Message);
                        _logger.LogWarning(ex, "PDM Commit schema unavailable. Business push completed without commit snapshot.");
                    }

                    if (warnings.Count > 0)
                        result.Warnings = warnings;
                }
                else
                {
                    result.Success = false;
                    result.LiveDataUpdated = false;
                    result.StagingOnly = false;
                    var errorLines = new List<string>();
                    var failedParts = partResults.Where(r => !r.Success).Select(r => $"- {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}").ToList();
                    if (failedParts.Count > 0)
                    {
                        errorLines.Add("Parts:");
                        errorLines.AddRange(failedParts);
                    }
                    var failedCads = cadResults.Where(r => !r.Success).Select(r => $"- {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}").ToList();
                    if (failedCads.Count > 0)
                    {
                        errorLines.Add("CADs:");
                        errorLines.AddRange(failedCads);
                    }
                    var failedDocs = docResults.Where(r => !r.Success).Select(r => $"- {r.SourceKey ?? "(unknown)"}: {r.ErrorMessage ?? "Unknown error"}").ToList();
                    if (failedDocs.Count > 0)
                    {
                        errorLines.Add("Documents:");
                        errorLines.AddRange(failedDocs);
                    }
                    if (bomFailures.Count > 0)
                    {
                        errorLines.Add("BOM relationships:");
                        errorLines.AddRange(bomFailures.Select(f => $"- {f}"));
                    }
                    result.ErrorMessage = "Business item push failed." +
                        (errorLines.Count > 0
                            ? Environment.NewLine + string.Join(Environment.NewLine, errorLines)
                            : string.Empty);
                }

                result.BomResults = bomResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDM push failed");
                result.Success = false;
                result.LiveDataUpdated = false;
                result.StagingOnly = false;
                result.ErrorMessage = ex.Message;
            }

            result.PartResults = partResults;
            result.CadResults = cadResults;
            result.DocumentResults = docResults;
            return result;
        }

        public static bool IsLibraryReference(PdmPartRequest part)
        {
            return part.IsExternalReference ||
                string.Equals(part.SourceKind, "LibraryReference", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPartObsolete(string state)
        {
            return PartLifecyclePolicy.IsPartObsolete(state);
        }

        public static string ClassifyArasError(ArasOperationException ex)
        {
            switch (ex.ErrorCode)
            {
                case ArasErrorCode.AuthInvalid:
                case ArasErrorCode.AuthExpired:
                    return "Authentication failure. Please log in again.";
                case ArasErrorCode.PermissionDenied:
                    return "Permission denied. The current user does not have access to the requested Part.";
                case ArasErrorCode.PartNotFound:
                    return "Part not found on the server.";
                case ArasErrorCode.ServerUnavailable:
                    return "Server is unavailable. Check your connection to Aras.";
                default:
                    return null;
            }
        }

        private static string SanitizeForUser(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            var lower = message.ToLowerInvariant();
            if (lower.Contains("authorization") || lower.Contains("bearer") ||
                lower.Contains("soap-env") || lower.Contains("soap:") ||
                lower.Contains("password") || lower.Contains("token"))
            {
                return "An unexpected server error occurred.";
            }

            return message;
        }

        private async Task<PdmItemResult> CreateOrGetPartAsync(PdmPartRequest part, CancellationToken ct)
        {
            try
            {
                var isLibraryRef = IsLibraryReference(part);

                if (isLibraryRef && string.IsNullOrWhiteSpace(part.ExistingPartId))
                {
                    return new PdmItemResult
                    {
                        SourceKey = part.LogicalCode,
                        ItemNumber = part.PartNumber,
                        Success = false,
                        ErrorMessage = "Library reference requires an ExistingPartId. Use the Library dialog to select a Part before push."
                    };
                }

                if (!string.IsNullOrWhiteSpace(part.ExistingPartId))
                {
                    var existingAml = $"<Item type=\"Part\" action=\"get\" id=\"{EscapeAml(part.ExistingPartId)}\" select=\"id,item_number,name,state,config_id,major_rev\"/>";
                    JObject existingResponse;
                    try
                    {
                        existingResponse = await _aml.ApplyAmlAsync(existingAml, "get", "Part", part.ExistingPartId, ct).ConfigureAwait(false);
                    }
                    catch (ArasOperationException ex)
                    {
                        var categorized = ClassifyArasError(ex);
                        if (categorized != null)
                        {
                            return new PdmItemResult
                            {
                                SourceKey = part.LogicalCode,
                                ItemNumber = part.PartNumber,
                                Success = false,
                                ErrorMessage = "Part reuse failed: " + categorized
                            };
                        }

                        _logger.LogWarning(ex, "CreateOrGetPartAsync unexpected Aras error for ExistingPartId={PartId}", part.ExistingPartId);
                        return new PdmItemResult
                        {
                            SourceKey = part.LogicalCode,
                            ItemNumber = part.PartNumber,
                            Success = false,
                            ErrorMessage = "Part reuse failed due to an unexpected server error. Try again."
                        };
                    }

                    var item = existingResponse?["Items"]?[0];
                    var existingPartId = item?["id"]?.ToString();
                    if (item == null || string.IsNullOrWhiteSpace(existingPartId))
                    {
                        var msg = isLibraryRef
                            ? "Library reference reuse failed: the referenced Aras Part ID was not found."
                            : "Part reuse failed: the specified ExistingPartId was not found on the server.";
                        return new PdmItemResult
                        {
                            SourceKey = part.LogicalCode,
                            ItemNumber = part.PartNumber,
                            Success = false,
                            ErrorMessage = msg
                        };
                    }

                    var configId = item["config_id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(part.ExistingPartConfigId) &&
                        !string.Equals(configId, part.ExistingPartConfigId, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PdmItemResult
                        {
                            SourceKey = part.LogicalCode,
                            ItemNumber = part.PartNumber,
                            Success = false,
                            ErrorMessage = $"Part reuse failed: config_id mismatch. Expected '{part.ExistingPartConfigId}', found '{configId}'. The Library entry may refer to a different Part generation."
                        };
                    }

                    var majorRev = item["major_rev"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(part.ExistingPartRevision) &&
                        !string.Equals(majorRev, part.ExistingPartRevision, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PdmItemResult
                        {
                            SourceKey = part.LogicalCode,
                            ItemNumber = part.PartNumber,
                            Success = false,
                            ErrorMessage = $"Part reuse failed: revision mismatch. Expected '{part.ExistingPartRevision}', found '{majorRev ?? "(none)"}'. The Library entry may refer to a different Part revision."
                        };
                    }

                    var state = item["state"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(state) && IsPartObsolete(state))
                    {
                        var msg = PartLifecyclePolicy.GetPartNotReusableMessage(state, part.PartNumber ?? part.LogicalCode)
                                   ?? "Part reuse failed: the referenced Part cannot be reused.";
                        return new PdmItemResult
                        {
                            SourceKey = part.LogicalCode,
                            ItemNumber = part.PartNumber,
                            Success = false,
                            ErrorMessage = msg
                        };
                    }

                    return new PdmItemResult
                    {
                        SourceKey = part.LogicalCode,
                        ArasId = existingPartId,
                        ItemNumber = part.PartNumber,
                        Success = true,
                        ActionTaken = isLibraryRef ? "ReusedFromLibrary" : "Reused"
                    };
                }

                var existingId = await FindItemByNumberAsync("Part", part.PartNumber, ct);
                if (existingId != null)
                {
                    return new PdmItemResult
                    {
                        SourceKey = part.LogicalCode,
                        ArasId = existingId,
                        ItemNumber = part.PartNumber,
                        Success = true,
                        ActionTaken = "Reused"
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
                    ActionTaken = !string.IsNullOrWhiteSpace(newId) ? "Created" : null,
                    ErrorMessage = newId == null
                        ? $"Part add failed. number='{part.PartNumber}', classification='{part.Classification} (preview-only, not sent to Aras)', name='{part.Name ?? part.LogicalCode}'. Aras returned no id."
                        : null
                };
            }
            catch (ArasOperationException ex)
            {
                var categorized = ClassifyArasError(ex);
                return new PdmItemResult
                {
                    SourceKey = part.LogicalCode,
                    ItemNumber = part.PartNumber,
                    Success = false,
                    ErrorMessage = categorized ?? "Part operation failed: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateOrGetPartAsync unexpected error for PartNumber={PartNumber}", part.PartNumber);
                return new PdmItemResult
                {
                    SourceKey = part.LogicalCode,
                    ItemNumber = part.PartNumber,
                    Success = false,
                    ErrorMessage = "Part operation failed due to an unexpected error: " + ex.Message
                };
            }
        }

        private async Task<PdmItemResult> CreateOrGetCadAsync(PdmCadRequest cad, string linkedPartId, CancellationToken ct)
        {
            try
            {
                var existingId = await FindItemByNumberAsync("CAD", cad.CadNumber, ct);
                var isNew = string.IsNullOrWhiteSpace(existingId);
                var actionTaken = isNew ? "Created" : "Reused";
                string cadId;

                if (isNew)
                {
                    var aml = $"<Item type=\"CAD\" action=\"add\">" +
                        $"<item_number>{EscapeAml(cad.CadNumber)}</item_number>" +
                        $"<classification>{EscapeAml(cad.Classification)}</classification>" +
                        $"<authoring_tool>{EscapeAml(CadConstants.IronCadAuthoringTool)}</authoring_tool>" +
                        "<name>" + EscapeAml(cad.SourceFileName) + "</name>" +
                        "</Item>";

                    var response = await _aml.ApplyAmlAsync(aml, "add", "CAD", null, ct);
                    cadId = response?["id"]?.ToString();

                    if (string.IsNullOrWhiteSpace(cadId))
                    {
                        return new PdmItemResult
                        {
                            SourceKey = cad.SourceFileName,
                            ItemNumber = cad.CadNumber,
                            Success = false,
                            ErrorMessage = "CAD add returned no id from Aras"
                        };
                    }
                }
                else
                {
                    cadId = existingId;
                }

                await EnsureCadMetadataAsync(cadId, cad.Classification, ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(linkedPartId))
                {
                    await EnsureRelationshipAsync("Part CAD", linkedPartId, cadId, ct);
                }

                if (!string.IsNullOrWhiteSpace(cad.SourceFilePath))
                {
                    var uploadSuccess = await AttachNativeFileToCadAsync(cadId, cad.SourceFilePath, cad.SourceFileName, ct);
                    if (uploadSuccess)
                    {
                        actionTaken = isNew ? "Created+File" : "Reused+File";
                    }
                    else
                    {
                        return new PdmItemResult
                        {
                            SourceKey = cad.SourceFileName,
                            ArasId = cadId,
                            ItemNumber = cad.CadNumber,
                            Success = false,
                            ActionTaken = actionTaken,
                            ErrorMessage = "CAD metadata " + actionTaken.ToLowerInvariant() +
                                " but native file attach failed. Path: " + cad.SourceFilePath
                        };
                    }
                }

                return new PdmItemResult
                {
                    SourceKey = cad.SourceFileName,
                    ArasId = cadId,
                    ItemNumber = cad.CadNumber,
                    Success = true,
                    ActionTaken = actionTaken
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

        private async Task EnsureCadMetadataAsync(string cadId, string classification, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId))
                return;

            var aml = $"<Item type=\"CAD\" action=\"edit\" id=\"{EscapeAml(cadId)}\">" +
                $"<classification>{EscapeAml(string.IsNullOrWhiteSpace(classification) ? CadConstants.IronCadPartClassification : classification)}</classification>" +
                $"<authoring_tool>{EscapeAml(CadConstants.IronCadAuthoringTool)}</authoring_tool>" +
                "</Item>";

            await _aml.ApplyAmlAsync(aml, "edit", "CAD", cadId, ct).ConfigureAwait(false);
        }

        private async Task<bool> AttachNativeFileToCadAsync(string cadId, string filePath, string fileName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId) || string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Native file not found for CAD '{CadNumber}': {Path}", cadId, filePath);
                    return false;
                }

                var fileId = await _vault.UploadFileAsync(filePath, fileName, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(fileId))
                    return false;

                var aml = $"<Item type=\"CAD\" action=\"edit\" id=\"{EscapeAml(cadId)}\">" +
                    $"<native_file>{EscapeAml(fileId)}</native_file>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(aml, "edit", "CAD", cadId, ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to attach native file to CAD '{CadNumber}'", cadId);
                return false;
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
                        Success = true,
                        ActionTaken = "Reused"
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
                    ActionTaken = !string.IsNullOrWhiteSpace(newId) ? "Created" : null,
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

        private async Task<BomActionResult> EnsurePartBomAsync(string parentId, string childId, int quantity, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
                return BomActionResult.InvalidParentChild;

            if (string.Equals(parentId, childId, StringComparison.OrdinalIgnoreCase))
                return BomActionResult.InvalidParentChild;

            if (quantity < 1)
                return BomActionResult.InvalidQuantity;

            var existing = await FindPartBomInfoAsync(parentId, childId, ct).ConfigureAwait(false);
            if (!existing.Exists)
            {
                var relId = Guid.NewGuid().ToString("N").ToUpperInvariant();
                var aml = $"<Item type=\"Part BOM\" action=\"add\" id=\"{relId}\">" +
                    $"<source_id>{EscapeAml(parentId)}</source_id>" +
                    $"<related_id>{EscapeAml(childId)}</related_id>" +
                    $"<quantity>{quantity}</quantity>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(aml, "add", "Part BOM", null, ct).ConfigureAwait(false);
                return BomActionResult.Created;
            }

            if (existing.ExistingQuantity.HasValue &&
                existing.ExistingQuantity.Value != quantity &&
                !string.IsNullOrWhiteSpace(existing.RelationshipId))
            {
                await UpdatePartBomQuantityAsync(existing.RelationshipId, quantity, ct).ConfigureAwait(false);
                return BomActionResult.QuantityUpdated;
            }

            return BomActionResult.Unchanged;
        }

        private async Task<PdmBomExistenceInfo> FindPartBomInfoAsync(string parentId, string childId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
            {
                return new PdmBomExistenceInfo();
            }

            var aml = $"<Item type=\"Part BOM\" action=\"get\" select=\"id,quantity\">" +
                $"<source_id>{EscapeAml(parentId)}</source_id>" +
                $"<related_id>{EscapeAml(childId)}</related_id>" +
                "</Item>";

            var response = await _aml.ApplyAmlAsync(aml, "get", "Part BOM", null, ct).ConfigureAwait(false);
            var items = response?["Items"];
            if (items == null || !items.HasValues)
            {
                return new PdmBomExistenceInfo();
            }

            var quantityToken = items[0]?["quantity"]?.ToString();
            int parsedQuantity;
            return new PdmBomExistenceInfo
            {
                Exists = true,
                ExistingQuantity = int.TryParse(quantityToken, out parsedQuantity) ? parsedQuantity : (int?)null,
                RelationshipId = items[0]?["id"]?.ToString()
            };
        }

        private async Task UpdatePartBomQuantityAsync(string relationshipId, int quantity, CancellationToken ct)
        {
            var aml = $"<Item type=\"Part BOM\" action=\"edit\" id=\"{EscapeAml(relationshipId)}\">" +
                $"<quantity>{quantity}</quantity>" +
                "</Item>";

            await _aml.ApplyAmlAsync(aml, "edit", "Part BOM", relationshipId, ct).ConfigureAwait(false);
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

        // TODO(PERM-COMMIT-AUTHOR): Add <author> field from session user.
        // Currently not sent; server field exists but client never populates it.
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FindRelationshipAsync failed for relType={RelType} sourceId={SourceId} relatedId={RelatedId}", relType, sourceId, relatedId);
                return null;
            }
        }

        // TODO(PERM-COMMIT-FILE-VAULT): Add vault_file_id after file upload.
        // Without vault_file_id, CloneAsync cannot know which vault file to
        // download. Currently not sent.
        // TODO(PERM-COMMIT-FILE-CHANGE-TYPE): Derive change_type from diff
        // engine (added/modified/deleted). Currently hardcoded to "added".
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FindItemByNumberAsync failed for itemType={ItemType} itemNumber={ItemNumber}", itemType, itemNumber);
                return null;
            }
        }

        private async Task<ClonePartInfo> GetPartByNumberAsync(string itemNumber, CancellationToken ct)
        {
            try
            {
                var aml = $"<Item type=\"Part\" action=\"get\" select=\"id,item_number,name\">" +
                    $"<item_number>{EscapeAml(itemNumber)}</item_number>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "get", "Part", null, ct).ConfigureAwait(false);
                return MapPartInfo(response?["Items"]?[0]);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return null;
            }
        }

        private async Task<ClonePartInfo> GetPartByIdAsync(string partId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partId))
                return null;

            try
            {
                var response = await _aml.ApplyItemAsync("Part", partId, "get", "id,item_number,name", ct).ConfigureAwait(false);
                return MapPartInfo(response);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return null;
            }
        }

        private async Task<IReadOnlyList<string>> GetChildPartIdsAsync(string parentPartId, CancellationToken ct)
        {
            try
            {
                var aml = $"<Item type=\"Part BOM\" action=\"get\" select=\"related_id\">" +
                    $"<source_id>{EscapeAml(parentPartId)}</source_id>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "get", "Part BOM", null, ct).ConfigureAwait(false);
                var items = response?["Items"];
                if (items == null || !items.HasValues)
                    return Array.Empty<string>();

                var ids = new List<string>();
                foreach (var item in items)
                {
                    var relatedId = item?["related_id"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(relatedId))
                    {
                        ids.Add(relatedId);
                    }
                }

                return ids;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return Array.Empty<string>();
            }
        }

        private async Task<IReadOnlyList<CloneCadInfo>> GetPartCadCandidatesAsync(string partId, CancellationToken ct)
        {
            try
            {
                var relAml = $"<Item type=\"Part CAD\" action=\"get\" select=\"related_id\">" +
                    $"<source_id>{EscapeAml(partId)}</source_id>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(relAml, "get", "Part CAD", null, ct).ConfigureAwait(false);
                var items = response?["Items"];
                if (items == null || !items.HasValues)
                    return Array.Empty<CloneCadInfo>();

                var cads = new List<CloneCadInfo>();
                foreach (var item in items)
                {
                    var cadId = item?["related_id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(cadId))
                        continue;

                    JObject cadResponse;
                    try
                    {
                        cadResponse = await _aml.ApplyItemAsync("CAD", cadId, "get", "id,item_number,name,classification,authoring_tool,native_file,generation", ct).ConfigureAwait(false);
                    }
                    catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
                    {
                        continue;
                    }

                    var cadToken = cadResponse;
                    if (cadToken == null)
                        continue;

                    cads.Add(new CloneCadInfo
                    {
                        Id = cadToken["id"]?.ToString(),
                        ItemNumber = cadToken["item_number"]?.ToString(),
                        Name = cadToken["name"]?.ToString(),
                        Classification = cadToken["classification"]?.ToString(),
                        AuthoringTool = cadToken["authoring_tool"]?.ToString(),
                        NativeFileId = cadToken["native_file"]?.ToString()
                    });
                }

                return cads;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return Array.Empty<CloneCadInfo>();
            }
        }

        private async Task<IReadOnlyList<string>> GetRelatedDocumentNamesAsync(string relationshipType, string sourceId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return Array.Empty<string>();

            try
            {
                var relAml = $"<Item type=\"{EscapeAml(relationshipType)}\" action=\"get\" select=\"related_id\">" +
                    $"<source_id>{EscapeAml(sourceId)}</source_id>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(relAml, "get", relationshipType, null, ct).ConfigureAwait(false);
                var items = response?["Items"];
                if (items == null || !items.HasValues)
                    return Array.Empty<string>();

                var names = new List<string>();
                foreach (var item in items)
                {
                    var documentId = item?["related_id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(documentId))
                        continue;

                    JObject docResponse;
                    try
                    {
                        docResponse = await _aml.ApplyItemAsync("Document", documentId, "get", "id,name,item_number", ct).ConfigureAwait(false);
                    }
                    catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
                    {
                        continue;
                    }

                    var docToken = docResponse;
                    var documentName = docToken?["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(documentName))
                    {
                        names.Add(documentName);
                    }
                }

                return names;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return Array.Empty<string>();
            }
        }

        private static ClonePartInfo MapPartInfo(Newtonsoft.Json.Linq.JToken token)
        {
            if (token == null)
                return null;

            return new ClonePartInfo
            {
                Id = token["id"]?.ToString(),
                ItemNumber = token["item_number"]?.ToString(),
                Name = token["name"]?.ToString()
            };
        }

        private static CloneCadInfo SelectPreferredCad(IEnumerable<CloneCadInfo> candidates, string partNumber, bool isRootPart)
        {
            if (candidates == null)
                return null;

            var expectedNumbers = BuildExpectedCadNumbers(partNumber, isRootPart);
            var filtered = candidates
                .Where(cad => cad != null)
                .ToList();

            if (filtered.Count == 0)
                return null;

            var exactIronCad = filtered
                .Where(cad => string.Equals(cad.AuthoringTool, CadConstants.IronCadAuthoringTool, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (isRootPart)
            {
                var rootAssembly = exactIronCad.FirstOrDefault(cad =>
                    string.Equals(cad.Classification, IronCadAssemblyClassification, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(cad.NativeFileId));
                if (rootAssembly != null)
                    return rootAssembly;

                rootAssembly = filtered.FirstOrDefault(cad =>
                    !string.IsNullOrWhiteSpace(cad.NativeFileId) &&
                    expectedNumbers.Contains(cad.ItemNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase));
                if (rootAssembly != null)
                    return rootAssembly;
            }
            else
            {
                var partCad = exactIronCad.FirstOrDefault(cad =>
                    string.Equals(cad.Classification, CadConstants.IronCadPartClassification, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(cad.NativeFileId));
                if (partCad != null)
                    return partCad;

                partCad = filtered.FirstOrDefault(cad =>
                    !string.IsNullOrWhiteSpace(cad.NativeFileId) &&
                    expectedNumbers.Contains(cad.ItemNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase));
                if (partCad != null)
                    return partCad;
            }

            return filtered
                .OrderByDescending(cad => !string.IsNullOrWhiteSpace(cad.NativeFileId))
                .ThenByDescending(cad => string.Equals(cad.AuthoringTool, CadConstants.IronCadAuthoringTool, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(cad => string.Equals(cad.Classification, IronCadAssemblyClassification, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(cad => expectedNumbers.Contains(cad.ItemNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .ThenBy(cad => cad.ItemNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private async Task<CloneCadInfo> FindFallbackCadAsync(string partNumber, bool isRootPart, CancellationToken ct)
        {
            foreach (var cadNumber in BuildExpectedCadNumbers(partNumber, isRootPart))
            {
                var cadId = await FindItemByNumberAsync("CAD", cadNumber, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(cadId))
                    continue;

                try
                {
                    var cadToken = await _aml.ApplyItemAsync("CAD", cadId, "get", "id,item_number,name,classification,authoring_tool,native_file,generation", ct).ConfigureAwait(false);
                    if (cadToken == null)
                        continue;

                    return new CloneCadInfo
                    {
                        Id = cadToken["id"]?.ToString(),
                        ItemNumber = cadToken["item_number"]?.ToString(),
                        Name = cadToken["name"]?.ToString(),
                        Classification = cadToken["classification"]?.ToString(),
                        AuthoringTool = cadToken["authoring_tool"]?.ToString(),
                        NativeFileId = cadToken["native_file"]?.ToString()
                    };
                }
                catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
                {
                    continue;
                }
            }

            return null;
        }

        private static bool IsCadExpectedForPart(string partNumber, bool isRootPart)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                return false;

            if (isRootPart)
                return true;

            return partNumber.Count(ch => ch == '-') >= 2;
        }

        private static IReadOnlyList<string> BuildExpectedCadNumbers(string partNumber, bool isRootPart)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(partNumber))
                return result;

            if (isRootPart)
            {
                result.Add(partNumber + "-CAD-ASM");
                result.Add(partNumber + "-ICS");
                return result;
            }

            var match = System.Text.RegularExpressions.Regex.Match(
                partNumber,
                @"^(?<project>.+)-(?<group>\d{2})-(?<index>\d{2})$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                result.Add(match.Groups["project"].Value + "-CAD-" + match.Groups["group"].Value + "-" + match.Groups["index"].Value);
            }

            result.Add(partNumber + "-ICS");
            return result;
        }

        private static void EnsurePlaceholderFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, Array.Empty<byte>());
            }
        }

        private static int GeneratePackageShapeFiles(
            string repositoryCode,
            string projectFolder,
            string drawingsFolder,
            ClonePartInfo rootPart,
            IEnumerable<ClonePartInfo> allParts,
            IReadOnlyDictionary<string, CloneCadInfo> selectedCadByPartId)
        {
            var createdCount = 0;
            var parts = allParts?.Where(p => p != null).ToList() ?? new List<ClonePartInfo>();
            var partById = parts
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            string projectCode = repositoryCode;
            string version = "1.0";

            foreach (var cad in selectedCadByPartId?.Values ?? Array.Empty<CloneCadInfo>())
            {
                var fileName = cad?.Name ?? string.Empty;
                if (TryParseAssemblyCadName(fileName, out var parsedProject, out var parsedVersion))
                {
                    projectCode = parsedProject;
                    version = parsedVersion;
                    break;
                }

                if (TryParseDetailCadName(fileName, out parsedProject, out parsedVersion, out _))
                {
                    projectCode = parsedProject;
                    version = parsedVersion;
                }
            }

            createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, projectCode + "_Ver" + version + ".dwg"));

            foreach (var cad in selectedCadByPartId?.Values ?? Array.Empty<CloneCadInfo>())
            {
                var fileName = cad?.Name;
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var drawingName = Path.GetFileNameWithoutExtension(fileName) + ".dwg";
                createdCount += EnsurePlaceholderFileCreated(Path.Combine(drawingsFolder, drawingName));
            }

            var rootChildren = parts
                .Where(p => string.Equals(p.ParentId, rootPart.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => ExtractPartNumberTuple(p.ItemNumber).group)
                .ThenBy(p => ExtractPartNumberTuple(p.ItemNumber).index)
                .ToList();

            foreach (var groupPart in rootChildren)
            {
                var tuple = ExtractPartNumberTuple(groupPart.ItemNumber);
                var groupNumber = tuple.group <= 0 ? 0 : tuple.group;
                var groupName = NormalizePartDisplayName(groupPart.Name, groupPart.ItemNumber);
                if (groupNumber > 0)
                {
                    createdCount += EnsurePlaceholderFileCreated(Path.Combine(
                        projectFolder,
                        groupNumber.ToString("00") + ". " + groupName + ".pdf"));
                }

                var children = parts
                    .Where(p => string.Equals(p.ParentId, groupPart.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => ExtractPartNumberTuple(p.ItemNumber).index)
                    .ToList();

                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    var childTuple = ExtractPartNumberTuple(child.ItemNumber);
                    var childNumber = childTuple.index <= 0 ? i + 1 : childTuple.index;
                    var letter = (char)('A' + i);
                    var childName = NormalizePartDisplayName(child.Name, child.ItemNumber);
                    if (groupNumber > 0)
                    {
                        var pdfName = groupNumber.ToString("00") + letter + ". " +
                            groupName + "_" + childNumber.ToString("00") + "_" + childName + ".pdf";
                        createdCount += EnsurePlaceholderFileCreated(Path.Combine(projectFolder, pdfName));
                    }
                }
            }

            createdCount += EnsureStructureSummaryFile(projectFolder, projectCode, version, rootChildren, partById);
            return createdCount;
        }

        private static int EnsureStructureSummaryFile(
            string projectFolder,
            string projectCode,
            string version,
            IReadOnlyList<ClonePartInfo> rootChildren,
            IReadOnlyDictionary<string, ClonePartInfo> partById)
        {
            var path = Path.Combine(projectFolder, projectCode + "-STRUCTURE.txt");
            if (File.Exists(path))
                return 0;

            var lines = new List<string>
            {
                "Project   : " + projectCode,
                "Version   : " + version,
                "Generated : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                string.Empty,
                "Business structure:"
            };

            foreach (var groupPart in rootChildren)
            {
                var tuple = ExtractPartNumberTuple(groupPart.ItemNumber);
                lines.Add("  " + tuple.group.ToString("00") + ". " + NormalizePartDisplayName(groupPart.Name, groupPart.ItemNumber));

                var childParts = partById.Values
                    .Where(p => string.Equals(p.ParentId, groupPart.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => ExtractPartNumberTuple(p.ItemNumber).index)
                    .ToList();

                for (var i = 0; i < childParts.Count; i++)
                {
                    var child = childParts[i];
                    var childTuple = ExtractPartNumberTuple(child.ItemNumber);
                    lines.Add("    " + tuple.group.ToString("00") + (char)('A' + i) + ". " +
                        NormalizePartDisplayName(child.Name, child.ItemNumber) +
                        " (" + childTuple.index.ToString("00") + ")");
                }
            }

            File.WriteAllLines(path, lines);
            return 1;
        }

        private static int EnsurePlaceholderFileCreated(string path)
        {
            if (File.Exists(path))
                return 0;

            EnsurePlaceholderFile(path);
            return 1;
        }

        private static bool TryParseAssemblyCadName(string fileName, out string projectCode, out string version)
        {
            projectCode = null;
            version = null;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var match = Regex.Match(
                fileName,
                @"^Assembly-(?<project>.+)-Ver(?<version>\d+\.\d+)[A-Za-z].*\.ics$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            projectCode = match.Groups["project"].Value;
            version = match.Groups["version"].Value;
            return true;
        }

        private static bool TryParseDetailCadName(string fileName, out string projectCode, out string version, out int sequence)
        {
            projectCode = null;
            version = null;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var match = Regex.Match(
                fileName,
                @"^(?<project>.+)_Ver(?<version>\d+\.\d+)_(?<sequence>\d{3})\.ics$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            projectCode = match.Groups["project"].Value;
            version = match.Groups["version"].Value;
            int.TryParse(match.Groups["sequence"].Value, out sequence);
            return true;
        }

        private static (int group, int index) ExtractPartNumberTuple(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                return (0, 0);

            var match = Regex.Match(partNumber, @"-(?<group>\d{2})(?:-(?<index>\d{2}))?$");
            if (!match.Success)
                return (0, 0);

            var group = 0;
            var index = 0;
            int.TryParse(match.Groups["group"].Value, out group);
            if (match.Groups["index"].Success)
                int.TryParse(match.Groups["index"].Value, out index);

            return (group, index);
        }

        private static string NormalizePartDisplayName(string name, string fallback)
        {
            var value = string.IsNullOrWhiteSpace(name) ? fallback : name;
            if (string.IsNullOrWhiteSpace(value))
                return "Part";

            value = Regex.Replace(value, @"^\d+\s*", string.Empty).Trim();
            value = value.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            return value;
        }

        private sealed class ClonePartInfo
        {
            public string Id { get; set; }
            public string ItemNumber { get; set; }
            public string Name { get; set; }
            public string ParentId { get; set; }
        }

        private sealed class CloneCadInfo
        {
            public string Id { get; set; }
            public string ItemNumber { get; set; }
            public string Name { get; set; }
            public string Classification { get; set; }
            public string AuthoringTool { get; set; }
            public string NativeFileId { get; set; }
        }

        private void EnsureAuthenticated()
        {
            if (_aml == null)
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "HttpPdmRepositoryClient is not authenticated. Call SetSession() after login.");
        }

        /// <summary>
        /// ReviseCadAsync — creates a new major revision of a Released CAD and its linked Part
        /// by calling the server-side Aras method <c>idea_ReviseCad</c>.
        ///
        /// EXPECTED SERVER-SIDE METHOD BEHAVIOR:
        ///
        /// The server method <c>idea_ReviseCad</c> should be implemented as an Aras
        /// C# IOM server method and must:
        ///   1. Version the Part (create new major revision, same Part Number).
        ///   2. Version the CAD (create new major revision, same CAD Number).
        ///   3. Set the new CAD lifecycle state to "Khoi tao".
        ///   4. Link the new CAD to the new Part (Part CAD relationship).
        ///   5. Return an &lt;Item&gt; with the following attributes/properties:
        ///        - new_part_id (attribute or element)
        ///        - new_cad_id (attribute or element)
        ///        - new_revision (attribute or element)
        ///        - new_lifecycle_state (attribute or element)
        ///
        /// REQUEST SHAPE (sent via ApplyMethod):
        ///   &lt;Item type="Method" action="idea_ReviseCad"&gt;
        ///     &lt;cad_id&gt;{request.CadId}&lt;/cad_id&gt;
        ///     &lt;part_id&gt;{request.PartId}&lt;/part_id&gt;
        ///     &lt;part_number&gt;{request.PartNumber}&lt;/part_number&gt;
        ///     &lt;cad_number&gt;{request.CadNumber}&lt;/cad_number&gt;
        ///     &lt;reason&gt;{request.Reason}&lt;/reason&gt;
        ///   &lt;/Item&gt;
        ///
        /// CURRENT BEHAVIOR:
        /// Sends a real AML/SOAP request via <see cref="ArasAmlClient.ApplyMethodAsync"/>.
        /// If the server method does not exist (SOAP fault), returns a graceful
        /// failure with a descriptive <see cref="PdmReviseResult"/>.
        /// On success, parses the response and populates <see cref="PdmReviseResult"/>
        /// with the new IDs, revision, and lifecycle state.
        /// </summary>
        public async Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            EnsureAuthenticated();

            var parameters = new Dictionary<string, string>
            {
                ["cad_id"] = request.CadId,
                ["part_id"] = request.PartId,
                ["part_number"] = request.PartNumber,
                ["cad_number"] = request.CadNumber,
                ["reason"] = request.Reason
            };

            try
            {
                var response = await _aml.ApplyMethodAsync("idea_ReviseCad", parameters, ct)
                    .ConfigureAwait(false);

                var result = new PdmReviseResult();

                if (response == null || !response.HasValues)
                {
                    result.Success = false;
                    result.ErrorMessage = "Server returned an empty response.";
                    return result;
                }

                // Extract fields with resilient fallbacks for three response shapes:
                //
                // Shape A — properties as child elements (server uses setProperty):
                //   <Item type="CAD" id="cad123">
                //     <new_part_id>part456</new_part_id>
                //     <new_cad_id>cad789</new_cad_id>
                //     <new_revision>B</new_revision>
                //     <new_lifecycle_state>Khoi tao</new_lifecycle_state>
                //     ...
                //   </Item>
                //
                // Shape B — attributes on Item element:
                //   <Item type="CAD" id="cad123" new_part_id="part456" new_cad_id="cad789"
                //         new_revision="B" new_lifecycle_state="Khoi tao" />
                //
                // Shape C — minimal CAD item return (fallbacks):
                //   <Item type="CAD" id="cad789" major_rev="B" ... />
                //
                // Fallback chain:
                //   new_part_id:         explicit field → null (no "id" fallback — "id" is the CAD id, not Part id)
                //   new_cad_id:          explicit field → response Item's "id" attribute
                //   new_revision:        explicit field → "major_rev" property (present on CAD items)
                //   new_lifecycle_state: explicit field → "Khoi tao" (expected default)
                string newPartId = response.Value<string>("new_part_id");
                string newCadId = response.Value<string>("new_cad_id")
                    ?? response.Value<string>("id");
                string newRevision = response.Value<string>("new_revision")
                    ?? response.Value<string>("major_rev");
                string newLifecycleState = response.Value<string>("new_lifecycle_state")
                    ?? "Khoi tao";

                if (string.IsNullOrWhiteSpace(newCadId))
                {
                    result.Success = false;
                    result.ErrorMessage = "Server response is missing both 'new_cad_id' and 'id' attributes. Revise did not create a new CAD.";
                    return result;
                }

                result.Success = true;
                result.NewPartId = newPartId;
                result.NewCadId = newCadId;
                result.NewRevision = newRevision;
                result.NewLifecycleState = newLifecycleState;

                _logger.LogInformation(
                    "ReviseCadAsync succeeded for CAD '{CadNumber}' ({CadId}): NewPartId={NewPartId}, NewCadId={NewCadId}, NewRevision={NewRevision}",
                    request.CadNumber, request.CadId,
                    result.NewPartId, result.NewCadId, result.NewRevision);

                return result;
            }
            catch (ArasOperationException ex)
            {
                _logger.LogWarning(ex,
                    "ReviseCadAsync failed for CAD '{CadId}' / Part '{PartId}': {Error}",
                    request.CadId, request.PartId, ex.Message);

                return new PdmReviseResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ReviseCadAsync unexpected error for CAD '{CadId}' / Part '{PartId}': {Error}",
                    request.CadId, request.PartId, ex.Message);

                return new PdmReviseResult
                {
                    Success = false,
                    ErrorMessage = "Unexpected error: " + ex.Message
                };
            }
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
