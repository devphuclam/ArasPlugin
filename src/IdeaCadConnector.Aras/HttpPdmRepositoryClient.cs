using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging;

namespace IdeaCadConnector.Aras
{
    public sealed class HttpPdmRepositoryClient : IPdmRepositoryClient, IDisposable
    {
        private const string IronCadAssemblyClassification = "Mechanical/Assembly";
        private readonly ArasClientOptions _options;
        private readonly ILogger<HttpPdmRepositoryClient> _logger;
        private ArasHttpClient _http;
        private ArasAmlClient _aml;
        private VaultClient _vault;
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
                var id = await FindItemByNumberAsync("Part", number, ct).ConfigureAwait(false);
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
            var cadFolder = Path.Combine(projectFolder, "ARAS01");

            Directory.CreateDirectory(projectFolder);
            Directory.CreateDirectory(cadFolder);

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
            var downloadedCadCount = 0;
            var placeholderDocumentCount = 0;

            partQueue.Enqueue(rootPart);
            partIdsSeen.Add(rootPart.Id);

            while (partQueue.Count > 0)
            {
                var part = partQueue.Dequeue();

                var cadCandidates = await GetPartCadCandidatesAsync(part.Id, ct).ConfigureAwait(false);
                var selectedCad = SelectPreferredCad(cadCandidates, part.ItemNumber, string.Equals(part.Id, rootPart.Id, StringComparison.OrdinalIgnoreCase));
                if (selectedCad != null)
                {
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
                else
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

                var partsSucceeded = partResults.All(r => r.Success);
                var docsSucceeded = docResults.All(r => r.Success);
                var cadsMetadataSucceeded = cadResults.All(r => r.Success);
                var allBusinessSuccess = partsSucceeded && docsSucceeded;

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
                result.LiveDataUpdated = false;
                result.StagingOnly = false;
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

        private async Task<PdmBomExistenceInfo> FindPartBomInfoAsync(string parentId, string childId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
            {
                return new PdmBomExistenceInfo();
            }

            try
            {
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
                    ExistingQuantity = int.TryParse(quantityToken, out parsedQuantity) ? parsedQuantity : (int?)null
                };
            }
            catch
            {
                return new PdmBomExistenceInfo();
            }
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
            catch
            {
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
            catch
            {
                return null;
            }
        }

        private async Task<ClonePartInfo> GetPartByNumberAsync(string itemNumber, CancellationToken ct)
        {
            var aml = $"<Item type=\"Part\" action=\"get\" select=\"id,item_number,name\">" +
                $"<item_number>{EscapeAml(itemNumber)}</item_number>" +
                "</Item>";

            var response = await _aml.ApplyAmlAsync(aml, "get", "Part", null, ct).ConfigureAwait(false);
            return MapPartInfo(response?["Items"]?[0]);
        }

        private async Task<ClonePartInfo> GetPartByIdAsync(string partId, CancellationToken ct)
        {
            var aml = $"<Item type=\"Part\" action=\"get\" select=\"id,item_number,name\">" +
                $"<id>{EscapeAml(partId)}</id>" +
                "</Item>";

            var response = await _aml.ApplyAmlAsync(aml, "get", "Part", partId, ct).ConfigureAwait(false);
            return MapPartInfo(response?["Items"]?[0]);
        }

        private async Task<IReadOnlyList<string>> GetChildPartIdsAsync(string parentPartId, CancellationToken ct)
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

        private async Task<IReadOnlyList<CloneCadInfo>> GetPartCadCandidatesAsync(string partId, CancellationToken ct)
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

                var cadAml = $"<Item type=\"CAD\" action=\"get\" select=\"id,item_number,name,classification,authoring_tool,native_file,generation\">" +
                    $"<id>{EscapeAml(cadId)}</id>" +
                    "</Item>";

                var cadResponse = await _aml.ApplyAmlAsync(cadAml, "get", "CAD", cadId, ct).ConfigureAwait(false);
                var cadToken = cadResponse?["Items"]?[0];
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

        private async Task<IReadOnlyList<string>> GetRelatedDocumentNamesAsync(string relationshipType, string sourceId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return Array.Empty<string>();

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

                var docAml = $"<Item type=\"Document\" action=\"get\" select=\"id,name,item_number\">" +
                    $"<id>{EscapeAml(documentId)}</id>" +
                    "</Item>";

                var docResponse = await _aml.ApplyAmlAsync(docAml, "get", "Document", documentId, ct).ConfigureAwait(false);
                var docToken = docResponse?["Items"]?[0];
                var documentName = docToken?["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(documentName))
                {
                    names.Add(documentName);
                }
            }

            return names;
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

            var filtered = candidates
                .Where(cad => cad != null &&
                    string.Equals(cad.AuthoringTool, CadConstants.IronCadAuthoringTool, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count == 0)
                return null;

            if (isRootPart)
            {
                var rootAssembly = filtered.FirstOrDefault(cad =>
                    string.Equals(cad.Classification, IronCadAssemblyClassification, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(cad.NativeFileId));
                if (rootAssembly != null)
                    return rootAssembly;

                rootAssembly = filtered.FirstOrDefault(cad =>
                    !string.IsNullOrWhiteSpace(cad.ItemNumber) &&
                    cad.ItemNumber.EndsWith("-CAD-ASM", StringComparison.OrdinalIgnoreCase));
                if (rootAssembly != null)
                    return rootAssembly;
            }
            else
            {
                var partCad = filtered.FirstOrDefault(cad =>
                    string.Equals(cad.Classification, CadConstants.IronCadPartClassification, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(cad.NativeFileId));
                if (partCad != null)
                    return partCad;
            }

            return filtered
                .OrderByDescending(cad => !string.IsNullOrWhiteSpace(cad.NativeFileId))
                .ThenByDescending(cad => string.Equals(cad.Classification, IronCadAssemblyClassification, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(cad => !string.IsNullOrWhiteSpace(partNumber) &&
                    !string.IsNullOrWhiteSpace(cad.ItemNumber) &&
                    cad.ItemNumber.StartsWith(partNumber, StringComparison.OrdinalIgnoreCase))
                .ThenBy(cad => cad.ItemNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
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

        private sealed class ClonePartInfo
        {
            public string Id { get; set; }
            public string ItemNumber { get; set; }
            public string Name { get; set; }
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
            if (_http == null || _aml == null)
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "HttpPdmRepositoryClient is not authenticated. Call SetSession() after login.");
        }

        public Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _logger.LogWarning(
                "ReviseCadAsync called for CAD '{CadId}' / Part '{PartId}' but server-side revise method is not yet implemented.",
                request.CadId, request.PartId);

            var result = new PdmReviseResult
            {
                Success = false,
                ErrorMessage = "Server-side revise method not yet implemented. See the New Revision Guide for the manual revision path."
            };

            return Task.FromResult(result);
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
