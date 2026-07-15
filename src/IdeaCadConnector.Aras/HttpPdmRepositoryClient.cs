using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Workspace.Clone;
using IdeaCadConnector.Workspace.NormalizeExport;
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
        private IVaultFileClient _vault;
        private readonly bool _vaultClientInjected;
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

        internal HttpPdmRepositoryClient(
            ArasClientOptions options,
            IArasAmlClient amlClient,
            IVaultFileClient vaultClient,
            ILogger<HttpPdmRepositoryClient> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _aml = amlClient ?? throw new ArgumentNullException(nameof(amlClient));
            _vault = vaultClient ?? throw new ArgumentNullException(nameof(vaultClient));
            _vaultClientInjected = true;
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
            if (!_vaultClientInjected)
            {
                _vault = new VaultClient(_http, _options);
            }
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
            var projectFolder = Path.GetFullPath(request.TargetFolder.Trim());
            var cadFolder = Path.Combine(projectFolder, "cad");
            var tempRoot = Path.Combine(Path.GetTempPath(), "IdeaPdmClone", Guid.NewGuid().ToString("N"));
            var tempCadFolder = Path.Combine(tempRoot, "cad");
            string destinationStagingRoot = null;

            try
            {
                Directory.CreateDirectory(projectFolder);
                Directory.CreateDirectory(tempCadFolder);

                var conflict = FindCloneDestinationConflict(projectFolder);
                if (conflict != null)
                    return CloneFailure(repositoryCode, projectFolder, warnings, "Clone destination already contains '" + conflict + "'.");

                var rootPart = await GetPartByNumberAsync(repositoryCode, ct).ConfigureAwait(false);
                if (rootPart == null)
                {
                    return CloneFailure(
                        repositoryCode,
                        projectFolder,
                        warnings,
                        "Root Part not found on Aras for repository '" + repositoryCode + "'.");
                }

                if (!string.IsNullOrWhiteSpace(request.BranchName) &&
                    !string.Equals(request.BranchName, "main", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add("Clone currently uses latest live data on Aras. Branch '" + request.BranchName + "' is local-only and was not resolved on server.");
                }

                var partQueue = new Queue<ClonePartInfo>();
                var partIdsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var canonicalPartIdByReference = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var nodes = new List<PdmCloneNode>();
                var edges = new List<PdmCloneBomEdge>();
                var downloadedCadCount = 0;
                string rootNativeFileName = null;

                partQueue.Enqueue(rootPart);
                partIdsSeen.Add(rootPart.Id);
                canonicalPartIdByReference[rootPart.Id] = rootPart.Id;

                while (partQueue.Count > 0)
                {
                    var part = partQueue.Dequeue();
                    var isRootPart = string.Equals(part.Id, rootPart.Id, StringComparison.OrdinalIgnoreCase);
                    var cadLookupDiagnostics = new List<string>();
                    var cadCandidates = await GetPartCadCandidatesAsync(part.Id, ct).ConfigureAwait(false);
                    cadLookupDiagnostics.Add("Part CAD candidates: " + cadCandidates.Count + ".");
                    var selectedCad = SelectPreferredCad(cadCandidates, part.ItemNumber, isRootPart);
                    if (selectedCad == null)
                        selectedCad = await FindFallbackCadAsync(part.ItemNumber, isRootPart, cadLookupDiagnostics, ct).ConfigureAwait(false);

                    if (selectedCad == null)
                        return CloneFailure(repositoryCode, projectFolder, warnings,
                            "No usable IronCAD record found for Part '" + part.ItemNumber + "'. " +
                            string.Join(" ", cadLookupDiagnostics), rootPart);
                    if (string.IsNullOrWhiteSpace(selectedCad.NativeFileId))
                        return CloneFailure(repositoryCode, projectFolder, warnings, "CAD '" + selectedCad.ItemNumber + "' exists but has no native file on Aras.", rootPart);

                    var downloadedPath = await _vault.DownloadFileAsync(selectedCad.NativeFileId, tempCadFolder, ct).ConfigureAwait(false);
                    var nativeFileName = ValidateDownloadedNativeFile(downloadedPath, tempCadFolder, selectedCad.Name);
                    var nameParts = ParseCanonicalNativeFileName(nativeFileName);
                    if (isRootPart && !string.Equals(nameParts.ItemCode, "ROOT", StringComparison.Ordinal))
                        return CloneFailure(repositoryCode, projectFolder, warnings, "Root CAD native filename must use item code ROOT.", rootPart);

                    nodes.Add(new PdmCloneNode
                    {
                        NodeId = part.Id,
                        ItemCode = nameParts.ItemCode,
                        ItemType = MapCloneItemType(selectedCad.Classification),
                        DisplayName = nameParts.DisplayName,
                        Revision = string.IsNullOrWhiteSpace(part.MajorRevision) ? "A" : part.MajorRevision,
                        NativeFileName = nativeFileName
                    });
                    downloadedCadCount++;
                    if (isRootPart)
                        rootNativeFileName = nativeFileName;

                    var childEdges = await GetChildPartEdgesAsync(part.Id, ct).ConfigureAwait(false);
                    foreach (var childEdge in childEdges)
                    {
                        if (!canonicalPartIdByReference.TryGetValue(childEdge.ChildPartId, out var childNodeId))
                        {
                            var childPart = await GetPartByIdAsync(childEdge.ChildPartId, ct).ConfigureAwait(false);
                            if (childPart == null || string.IsNullOrWhiteSpace(childPart.Id))
                                return CloneFailure(repositoryCode, projectFolder, warnings, "Child Part id '" + childEdge.ChildPartId + "' could not be loaded during clone.", rootPart);

                            childNodeId = childPart.Id;
                            canonicalPartIdByReference[childEdge.ChildPartId] = childNodeId;
                            canonicalPartIdByReference[childNodeId] = childNodeId;
                            if (partIdsSeen.Add(childNodeId))
                                partQueue.Enqueue(childPart);
                        }

                        edges.Add(new PdmCloneBomEdge
                        {
                            ParentNodeId = part.Id,
                            ChildNodeId = childNodeId,
                            Quantity = childEdge.Quantity,
                            SortOrder = childEdge.SortOrder
                        });
                    }
                }

                var buildResult = new PdmClonePackageBuilder().Build(new PdmClonePackageInput
                {
                    PackageRoot = tempRoot,
                    ProjectCode = repositoryCode,
                    Revision = string.IsNullOrWhiteSpace(rootPart.MajorRevision) ? "A" : rootPart.MajorRevision,
                    BranchName = string.IsNullOrWhiteSpace(request.BranchName) ? "main" : request.BranchName.Trim(),
                    RootNodeId = rootPart.Id,
                    Nodes = nodes,
                    Edges = edges
                });
                if (!buildResult.Success)
                    return CloneFailure(repositoryCode, projectFolder, warnings, buildResult.ErrorMessage, rootPart);

                try
                {
                    var projectParent = Directory.GetParent(projectFolder);
                    if (projectParent == null)
                        throw new IOException("Clone target must have a parent directory for atomic publication.");
                    destinationStagingRoot = Path.Combine(
                        projectParent.FullName,
                        ".idea-pdm-clone-" + Guid.NewGuid().ToString("N"));
                    PublishClonePackage(tempRoot, destinationStagingRoot, projectFolder);
                }
                catch (Exception ex)
                {
                    return CloneFailure(repositoryCode, projectFolder, warnings, "Clone package publication failed: " + ex.Message, rootPart);
                }

                return new PdmCloneResult
                {
                    Success = true,
                    RepositoryCode = repositoryCode,
                    RootPartId = rootPart.Id,
                    RootPartNumber = rootPart.ItemNumber,
                    ResolvedProjectFolder = projectFolder,
                    ResolvedCadFolder = cadFolder,
                    RootCadFilePath = Path.Combine(cadFolder, rootNativeFileName),
                    DownloadedCadFileCount = downloadedCadCount,
                    PlaceholderDocumentCount = 0,
                    Warnings = warnings
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return CloneFailure(repositoryCode, projectFolder, warnings, "Clone failed: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(destinationStagingRoot) && Directory.Exists(destinationStagingRoot))
                        Directory.Delete(destinationStagingRoot, true);
                }
            }
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

                if ((request.Cads ?? Array.Empty<PdmCadRequest>()).Any(c => !string.IsNullOrWhiteSpace(c.SourceFilePath)))
                    await EnsureVaultConfiguredAsync(ct).ConfigureAwait(false);

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
                var allBusinessSuccess = partsSucceeded && docsSucceeded && cadsMetadataSucceeded && !hasBomFailure;

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
                    var uploadError = await AttachNativeFileToCadAsync(cadId, cad.SourceFilePath, cad.SourceFileName, ct);
                    if (uploadError == null)
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
                                " but native file attach failed: " + uploadError +
                                " Path: " + cad.SourceFilePath
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

        private async Task<string> AttachNativeFileToCadAsync(string cadId, string filePath, string fileName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId) || string.IsNullOrWhiteSpace(filePath))
                return "CAD id or native file path is missing.";

            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("Native file not found for CAD '{CadNumber}': {Path}", cadId, filePath);
                    return "Native file was not found.";
                }

                var fileId = await _vault.UploadFileAsync(filePath, fileName, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(fileId))
                    return "Vault upload returned no File id.";

                var aml = $"<Item type=\"CAD\" action=\"edit\" id=\"{EscapeAml(cadId)}\">" +
                    $"<native_file>{EscapeAml(fileId)}</native_file>" +
                    "</Item>";

                await _aml.ApplyAmlAsync(aml, "edit", "CAD", cadId, ct).ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to attach native file to CAD '{CadNumber}'", cadId);
                return SanitizeForUser(ex.Message);
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

        private async Task<string> FindItemByNumberAsync(
            string itemType,
            string itemNumber,
            CancellationToken ct,
            ICollection<string> diagnostics = null)
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
                {
                    var resolvedId = items[0]?["id"]?.ToString();
                    diagnostics?.Add(itemType + " '" + itemNumber + "' resolved to id '" + (resolvedId ?? "(blank)") + "'.");
                    return resolvedId;
                }

                diagnostics?.Add(itemType + " '" + itemNumber + "' was not found.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FindItemByNumberAsync failed for itemType={ItemType} itemNumber={ItemNumber}", itemType, itemNumber);
                diagnostics?.Add(itemType + " '" + itemNumber + "' lookup failed: " + SanitizeForUser(ex.Message));
                return null;
            }
        }

        private static PdmCloneResult CloneFailure(
            string repositoryCode,
            string projectFolder,
            IReadOnlyList<string> warnings,
            string message,
            ClonePartInfo rootPart = null)
        {
            return new PdmCloneResult
            {
                Success = false,
                RepositoryCode = repositoryCode,
                RootPartId = rootPart?.Id,
                RootPartNumber = rootPart?.ItemNumber,
                ResolvedProjectFolder = projectFolder,
                ResolvedCadFolder = Path.Combine(projectFolder, "cad"),
                PlaceholderDocumentCount = 0,
                ErrorMessage = message,
                Warnings = warnings
            };
        }

        private static string FindCloneDestinationConflict(string projectFolder)
        {
            foreach (var name in new[] { "cad", ".idea-pdm", "pdm-bom-manifest.json" })
            {
                var path = Path.Combine(projectFolder, name);
                if (Directory.Exists(path) || File.Exists(path))
                    return name;
            }

            return null;
        }

        private static string ValidateDownloadedNativeFile(string downloadedPath, string targetDirectory, string storedCadName)
        {
            if (string.IsNullOrWhiteSpace(storedCadName))
                throw new InvalidOperationException("CAD Name must contain the canonical native filename.");
            if (!IsCanonicalNativeFileName(storedCadName))
                throw new InvalidOperationException("CAD Name must use <PROJECT>__<ITEMCODE>__<DISPLAY>.ics.");
            if (string.IsNullOrWhiteSpace(downloadedPath))
                throw new InvalidOperationException("Vault download did not return a local native file path.");

            var fullTargetDirectory = Path.GetFullPath(targetDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullDownloadedPath = Path.GetFullPath(downloadedPath);
            var downloadedDirectory = Path.GetDirectoryName(fullDownloadedPath)?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var nativeFileName = Path.GetFileName(fullDownloadedPath);

            if (!string.Equals(downloadedDirectory, fullTargetDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(fullDownloadedPath, Path.Combine(fullTargetDirectory, nativeFileName), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Vault download returned a path outside the temporary cad folder.");
            }
            if (!File.Exists(fullDownloadedPath))
                throw new InvalidOperationException("Vault download did not create the native CAD file.");
            if (!string.Equals(Path.GetExtension(nativeFileName), ".ics", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Vault native filename must be a filename-only safe .ics name.");

            ParseCanonicalNativeFileName(nativeFileName);
            if (!string.Equals(nativeFileName, storedCadName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Vault native filename '" + nativeFileName + "' does not match CAD Name '" + storedCadName + "'.");
            }

            return nativeFileName;
        }

        private static CloneNativeName ParseCanonicalNativeFileName(string nativeFileName)
        {
            if (string.IsNullOrWhiteSpace(nativeFileName) ||
                !string.Equals(nativeFileName, Path.GetFileName(nativeFileName), StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(nativeFileName), ".ics", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Vault native filename must be a filename-only safe canonical .ics name.");
            }

            var stem = Path.GetFileNameWithoutExtension(nativeFileName);
            var pieces = stem.Split(new[] { "__" }, StringSplitOptions.None);
            if (pieces.Length != 3 || pieces.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Vault native filename must use <PROJECT>__<ITEMCODE>__<DISPLAY>.ics.");

            string canonical;
            try
            {
                canonical = PdmNameNormalizer.CreateCanonicalFileName(pieces[0], "PRT", pieces[1], pieces[2]);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("Vault native filename is not canonical: " + ex.Message, ex);
            }

            if (!string.Equals(canonical, nativeFileName, StringComparison.Ordinal))
                throw new InvalidOperationException("Vault native filename is not canonical: " + nativeFileName);

            return new CloneNativeName
            {
                ItemCode = pieces[1],
                DisplayName = pieces[2]
            };
        }

        private static bool IsCanonicalNativeFileName(string nativeFileName)
        {
            try
            {
                ParseCanonicalNativeFileName(nativeFileName);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string MapCloneItemType(string classification)
        {
            if (string.Equals(classification, IronCadAssemblyClassification, StringComparison.OrdinalIgnoreCase))
                return "ASM";
            if (string.Equals(classification, CadConstants.IronCadPartClassification, StringComparison.OrdinalIgnoreCase))
                return "PRT";
            throw new InvalidOperationException("CAD classification must be Mechanical/Assembly or Mechanical/Part.");
        }

        private static void PublishClonePackage(
            string tempRoot,
            string destinationStagingRoot,
            string projectFolder)
        {
            var publishedPaths = new List<string>();
            try
            {
                if (Directory.Exists(destinationStagingRoot) || File.Exists(destinationStagingRoot))
                    throw new IOException("Clone destination staging path already exists.");
                Directory.CreateDirectory(destinationStagingRoot);
                StageClonePackage(tempRoot, destinationStagingRoot);

                foreach (var name in new[] { "cad", ".idea-pdm" })
                {
                    var source = Path.Combine(destinationStagingRoot, name);
                    var destination = Path.Combine(projectFolder, name);
                    if (Directory.Exists(destination) || File.Exists(destination))
                        throw new IOException("Clone destination already contains '" + name + "'.");
                    Directory.Move(source, destination);
                    publishedPaths.Add(destination);
                }

                var manifestDestination = Path.Combine(projectFolder, "pdm-bom-manifest.json");
                if (Directory.Exists(manifestDestination) || File.Exists(manifestDestination))
                    throw new IOException("Clone destination already contains 'pdm-bom-manifest.json'.");
                File.Move(
                    Path.Combine(destinationStagingRoot, "pdm-bom-manifest.json"),
                    manifestDestination);
                publishedPaths.Add(manifestDestination);
            }
            catch
            {
                for (var index = publishedPaths.Count - 1; index >= 0; index--)
                {
                    var path = publishedPaths[index];
                    if (File.Exists(path))
                        File.Delete(path);
                    else if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
                throw;
            }
        }

        private static void StageClonePackage(string tempRoot, string destinationStagingRoot)
        {
            foreach (var name in new[] { "cad", ".idea-pdm" })
            {
                CopyDirectory(
                    Path.Combine(tempRoot, name),
                    Path.Combine(destinationStagingRoot, name));
            }

            File.Copy(
                Path.Combine(tempRoot, "pdm-bom-manifest.json"),
                Path.Combine(destinationStagingRoot, "pdm-bom-manifest.json"),
                false);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
            {
                File.Copy(
                    sourceFile,
                    Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)),
                    false);
            }

            foreach (var sourceChildDirectory in Directory.EnumerateDirectories(sourceDirectory))
            {
                CopyDirectory(
                    sourceChildDirectory,
                    Path.Combine(destinationDirectory, Path.GetFileName(sourceChildDirectory)));
            }
        }

        private async Task<ClonePartInfo> GetPartByNumberAsync(string itemNumber, CancellationToken ct)
        {
            try
            {
                var aml = $"<Item type=\"Part\" action=\"get\" select=\"id,item_number,name,major_rev\">" +
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
                var response = await _aml.ApplyItemAsync("Part", partId, "get", "id,item_number,name,major_rev", ct).ConfigureAwait(false);
                return MapPartInfo(response);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return null;
            }
        }

        private async Task<IReadOnlyList<CloneBomEdge>> GetChildPartEdgesAsync(string parentPartId, CancellationToken ct)
        {
            try
            {
                var aml = $"<Item type=\"Part BOM\" action=\"get\" select=\"id,related_id,quantity,sort_order\">" +
                    $"<source_id>{EscapeAml(parentPartId)}</source_id>" +
                    "</Item>";

                var response = await _aml.ApplyAmlAsync(aml, "get", "Part BOM", null, ct).ConfigureAwait(false);
                var items = response?["Items"];
                if (items == null || !items.HasValues)
                    return Array.Empty<CloneBomEdge>();

                var edges = new List<CloneBomEdge>();
                for (var index = 0; index < items.Count(); index++)
                {
                    var item = items[index];
                    var relatedId = item?["related_id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(relatedId))
                        continue;

                    if (!decimal.TryParse(item?["quantity"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
                        quantity = 1m;
                    if (!int.TryParse(item?["sort_order"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sortOrder) || sortOrder <= 0)
                        sortOrder = (index + 1) * 10;

                    edges.Add(new CloneBomEdge
                    {
                        ChildPartId = relatedId,
                        Quantity = quantity,
                        SortOrder = sortOrder
                    });
                }

                return edges;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return Array.Empty<CloneBomEdge>();
            }
        }

        private async Task<IReadOnlyList<CloneCadInfo>> GetPartCadCandidatesAsync(string partId, CancellationToken ct)
        {
            var cads = new List<CloneCadInfo>();
            foreach (var relationshipType in new[] { "Part CAD" })
            {
                var relAml = $"<Item type=\"{relationshipType}\" action=\"get\" select=\"related_id\">" +
                    $"<source_id>{EscapeAml(partId)}</source_id>" +
                    "</Item>";

                JObject response;
                try
                {
                    response = await _aml.ApplyAmlAsync(relAml, "get", relationshipType, null, ct).ConfigureAwait(false);
                }
                catch (ArasOperationException ex) when (IsCadRelationshipTypeUnavailable(ex, relationshipType))
                {
                    _logger.LogDebug(ex, "CAD relationship type '{RelationshipType}' is unavailable; trying fallback lookup.", relationshipType);
                    continue;
                }

                var items = response?["Items"];
                if (items == null || !items.HasValues)
                    continue;

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

                if (cads.Count > 0)
                    return cads;
            }

            return cads;
        }

        private static bool IsCadRelationshipTypeUnavailable(ArasOperationException ex, string relationshipType)
        {
            if (ex == null || string.IsNullOrWhiteSpace(relationshipType))
                return false;

            if (ex.ErrorCode != ArasErrorCode.ValidationFailed &&
                ex.ErrorCode != ArasErrorCode.CadNotFound &&
                ex.ErrorCode != ArasErrorCode.UnexpectedServerError)
            {
                return false;
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf(relationshipType, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (message.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("not available", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("failed to get", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static ClonePartInfo MapPartInfo(Newtonsoft.Json.Linq.JToken token)
        {
            if (token == null)
                return null;

            return new ClonePartInfo
            {
                Id = token["id"]?.ToString(),
                ItemNumber = token["item_number"]?.ToString(),
                Name = token["name"]?.ToString(),
                MajorRevision = token["major_rev"]?.ToString()
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

        private async Task<CloneCadInfo> FindFallbackCadAsync(
            string partNumber,
            bool isRootPart,
            ICollection<string> diagnostics,
            CancellationToken ct)
        {
            foreach (var cadNumber in BuildExpectedCadNumbers(partNumber, isRootPart))
            {
                var cadId = await FindItemByNumberAsync("CAD", cadNumber, ct, diagnostics).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(cadId))
                    continue;

                try
                {
                    var cadToken = await _aml.ApplyItemAsync("CAD", cadId, "get", "id,item_number,name,classification,authoring_tool,native_file,generation", ct).ConfigureAwait(false);
                    if (cadToken == null)
                    {
                        diagnostics?.Add("CAD id '" + cadId + "' returned no item data.");
                        continue;
                    }

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
                    diagnostics?.Add("CAD id '" + cadId + "' could not be loaded: " + SanitizeForUser(ex.Message));
                    continue;
                }
            }

            return null;
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

            var itemCodeMatch = System.Text.RegularExpressions.Regex.Match(
                partNumber,
                @"^(?<project>.+)-(?<code>(?:[A-Z]\d{2}|PRT-\d{3}))$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (itemCodeMatch.Success)
            {
                result.Add(itemCodeMatch.Groups["project"].Value + "-CAD-" + itemCodeMatch.Groups["code"].Value);
            }

            result.Add(partNumber + "-ICS");
            return result;
        }

        private sealed class ClonePartInfo
        {
            public string Id { get; set; }
            public string ItemNumber { get; set; }
            public string Name { get; set; }
            public string MajorRevision { get; set; }
        }

        private sealed class CloneBomEdge
        {
            public string ChildPartId { get; set; }
            public decimal Quantity { get; set; }
            public int SortOrder { get; set; }
        }

        private sealed class CloneNativeName
        {
            public string ItemCode { get; set; }
            public string DisplayName { get; set; }
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

        private async Task EnsureVaultConfiguredAsync(CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_options.VaultId))
                return;

            var response = await _aml.ApplyAmlAsync(
                "<Item type=\"Vault\" action=\"get\" select=\"id,name,is_default\" />",
                "get", "Vault", null, ct).ConfigureAwait(false);
            var vaultId = ResolveVaultId(response);
            if (string.IsNullOrWhiteSpace(vaultId))
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "No Aras Vault is available for native CAD file upload. Configure 'aras.vaultId' or grant read access to the default Vault.");

            _options.VaultId = vaultId;
            if (_http != null)
                _vault = new VaultClient(_http, _options);
            _logger.LogInformation("Resolved the default Aras Vault for native CAD upload.");
        }

        internal static string ResolveVaultId(JObject response)
        {
            var items = response?["Items"] as JArray;
            var candidates = items != null
                ? items.OfType<JObject>().ToList()
                : response == null ? new List<JObject>() : new List<JObject> { response };

            var selected = candidates.FirstOrDefault(v =>
                    string.Equals(v["is_default"]?.ToString(), "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v["is_default"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v =>
                    string.Equals(v["name"]?.ToString(), "Default", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault();
            return selected?["id"]?.ToString();
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
