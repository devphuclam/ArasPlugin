using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aras.IOM;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Localization;
using Microsoft.Extensions.Logging;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Hybrid IArasCadClient implementation that uses IOM.dll for all CRUD,
    /// server-method, and file operations, while delegating Part search to
    /// <see cref="PartSearchClient"/> (OData).
    /// </summary>
    public sealed class ArasCadClient : IArasCadClient, IDisposable
    {
        private const string StartDetailedDesignMethodName = "idea_StartDetailedDesign";
        private const string SubmitCadForReviewMethodName = "idea_SubmitCadForReview";
        private const string ApproveCadReviewMethodName = "idea_ApproveCadReview";
        private const string RequestCadReworkMethodName = "idea_RequestCadRework";

        private readonly ArasClientOptions _options;
        private readonly ILogger<ArasCadClient> _logger;
        private readonly WorkflowActionMapper _actionMapper;
        private ArasAuthenticator _authenticator;
        private PartSearchClient _partSearch;
        private bool _disposed;

        public ArasCadClient(ArasClientOptions options, ILogger<ArasCadClient> logger = null)
            : this(options, logger, null)
        {
        }

        public ArasCadClient(ArasClientOptions options, ILogger<ArasCadClient> logger, WorkflowActionMapper actionMapper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArasCadClient>.Instance;
            _actionMapper = actionMapper ?? WorkflowActionMapper.CreateDefault();
        }

        // ---- IArasCadClient ---------------------------------------------------

        public async Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // --- 1. IOM authentication ---
            _authenticator = new ArasAuthenticator(_options);
            await _authenticator.LoginAsync(
                request.UserName,
                request.Password,
                request.Database ?? _options.Database,
                ct);

            // --- 2. Set up OData Part search client ---
            var httpClient = new HttpClient
            {
                BaseAddress = _options.BaseUri,
                Timeout = _options.Timeout
            };
            _partSearch = new PartSearchClient(httpClient, _options);

            if (!string.IsNullOrWhiteSpace(_authenticator.AccessToken))
                _partSearch.SetBearerToken(_authenticator.AccessToken, _authenticator.TokenType);

            return new ArasLoginResult
            {
                SessionToken = _authenticator.AccessToken,
                TokenType = _authenticator.TokenType,
                UserName = request.UserName,
                DisplayName = request.UserName,
                Database = request.Database ?? _options.Database
            };
        }

        public async Task<PartSearchResponse> SearchPartsAsync(
            PartSearchRequest request,
            CancellationToken ct)
        {
            EnsureAuthenticated();
            var (items, totalCount) = await _partSearch.SearchAsync(request, ct);
            return new PartSearchResponse(items, totalCount);
        }

        public async Task<CreateCadResult> CreateCadAsync(
            CreateCadRequest request,
            CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.PartId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "PartId is required.");

            if (CadNodeHelper.IsAssemblyClassification(request.PartClassification))
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "Cannot create a component-style primary CAD for an assembly-classified Part. Root assembly CAD is managed by the assembly mapping/push flow.");

            EnsureAuthenticated();

            var result = await RunIomAsync(() =>
            {
                var methodItem = _authenticator.Innovator.newItem("Method", "idea_EnsurePrimaryIronCadPartCad");
                methodItem.setProperty("part_id", request.PartId);
                return methodItem.apply();
            }, ct);

            CheckIomError(result, "EnsurePrimaryIronCadPartCad");

            return new CreateCadResult
            {
                Cad = MapCadFromItem(result)
            };
        }

        public async Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for checkout.");

            EnsureAuthenticated();

            _logger.LogDebug("Checkout (lock) cadId={CadId}", request.CadId);

            var currentCadItem = await RunIomAsync(() =>
            {
                var getItem = _authenticator.Innovator.newItem("CAD", "get");
                getItem.setID(request.CadId);
                getItem.setAttribute("select", CadSelectFields.CadFull);
                return getItem.apply();
            }, ct);

            CheckIomError(currentCadItem, "CAD lifecycle check before lock");

            var currentCad = MapCadFromItem(currentCadItem);
            EnsureCadCanBeCheckedOut(currentCad);

            // IOM: lock the CAD item
            var lockResult = await RunIomAsync(() =>
            {
                var lockItem = _authenticator.Innovator.newItem("CAD", "lock");
                lockItem.setID(request.CadId);
                return lockItem.apply();
            }, ct);

            CheckIomError(lockResult, "CAD lock");

            // Fetch the locked CAD with full property set
            var cadItem = await RunIomAsync(() =>
            {
                var getItem = _authenticator.Innovator.newItem("CAD", "get");
                getItem.setID(request.CadId);
                getItem.setAttribute("select", CadSelectFields.CadFull);
                return getItem.apply();
            }, ct);

            CheckIomError(cadItem, "CAD get after lock");

            return new CadCheckoutResult
            {
                CheckoutSessionId = null,  // IOM doesn't use transaction-based sessions
                LockToken = request.CadId, // The CAD id serves as the lock token
                Cad = MapCadFromItem(cadItem),
                IsReadOnly = false
            };
        }

        public async Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for open read-only.");

            EnsureAuthenticated();

            _logger.LogDebug("Open read-only cadId={CadId}", request.CadId);

            var cadItem = await RunIomAsync(() =>
            {
                var getItem = _authenticator.Innovator.newItem("CAD", "get");
                getItem.setID(request.CadId);
                getItem.setAttribute("select", CadSelectFields.CadFull);
                return getItem.apply();
            }, ct);

            CheckIomError(cadItem, "CAD get");

            return new CadCheckoutResult
            {
                CheckoutSessionId = null,
                LockToken = null,
                Cad = MapCadFromItem(cadItem),
                IsReadOnly = true
            };
        }

        public async Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.FilePath))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "FilePath is required for upload.");
            if (!File.Exists(request.FilePath))
                throw new ArasOperationException(ArasErrorCode.FileUploadNotFound, $"File not found: {request.FilePath}");

            EnsureAuthenticated();

            _logger.LogDebug("Upload file path={FilePath}", request.FilePath);

            var fileName = string.IsNullOrWhiteSpace(request.FileName)
                ? Path.GetFileName(request.FilePath)
                : request.FileName;

            var fileInfo = new FileInfo(request.FilePath);

            var fileItem = await RunIomAsync(() =>
            {
                var file = _authenticator.Innovator.newItem("File", "add");
                file.setProperty("filename", fileName);
                if (request.ContentType != null)
                    file.setProperty("mime_type", request.ContentType);
                file.attachPhysicalFile(request.FilePath);
                return file.apply();
            }, ct);

            CheckIomError(fileItem, "File upload");

            var uploadedFileId = fileItem.getID() ?? fileItem.getProperty("id", "");
            if (string.IsNullOrWhiteSpace(uploadedFileId))
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    "File upload succeeded but no file ID was returned.");

            return new FileUploadResult
            {
                UploadedFileId = uploadedFileId,
                FileName = fileName,
                SizeBytes = fileInfo.Length,
                Checksum = null // IOM does not expose the server-computed checksum
            };
        }

        public async Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for check-in.");
            if (string.IsNullOrWhiteSpace(request.UploadedFileId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "UploadedFileId is required for check-in.");

            EnsureAuthenticated();

            _logger.LogDebug("Check-in cadId={CadId} fileId={FileId}", request.CadId, request.UploadedFileId);

            var aml = $"<Item><cad_id>{EscapeAml(request.CadId)}</cad_id>" +
                      $"<uploaded_file_id>{EscapeAml(request.UploadedFileId)}</uploaded_file_id>";

            if (!string.IsNullOrWhiteSpace(request.Comment))
                aml += $"<comment>{EscapeAml(request.Comment)}</comment>";

            aml += "</Item>";

            var result = await RunIomAsync(() =>
                _authenticator.Innovator.applyMethod("idea_CommitCadCheckin", aml), ct);

            CheckIomError(result, "CommitCadCheckin");

            return new CadCheckinResult
            {
                Success = true,
                Cad = MapCadFromItem(result),
                Message = "Check-in completed successfully."
            };
        }

        public async Task<CancelCheckoutResult> CancelCheckoutAsync(
            CancelCheckoutRequest request,
            CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for cancel checkout.");

            EnsureAuthenticated();

            _logger.LogDebug("Cancel checkout (unlock) cadId={CadId}", request.CadId);

            var unlockResult = await RunIomAsync(() =>
            {
                var unlockItem = _authenticator.Innovator.newItem("CAD", "unlock");
                unlockItem.setID(request.CadId);
                return unlockItem.apply();
            }, ct);

            // If the item is not locked, unlock returns an error â€” treat as success
            if (unlockResult.isError())
            {
                var errMsg = unlockResult.getErrorString() ?? "";
                if (errMsg.Contains("locked", StringComparison.OrdinalIgnoreCase))
                {
                    // Item is not locked, which means checkout was already cancelled
                    _logger.LogInformation("Cancel checkout: CAD {CadId} was not locked.", request.CadId);
                }
                else
                {
                    CheckIomError(unlockResult, "CAD unlock");
                }
            }

            return new CancelCheckoutResult
            {
                Success = true,
                Message = "Checkout cancelled successfully."
            };
        }

        /// <summary>
        /// Download a native file from the vault to a local directory.
        /// This is a convenience method NOT on the <see cref="IArasCadClient"/>
        /// interface; callers that need file downloads can use it directly.
        /// </summary>
        /// <param name="fileId">The Aras File item ID to download.</param>
        /// <param name="targetDirectory">Local directory to save the file into.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The full path of the downloaded file.</returns>
        public async Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "FileId is required.");
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Target directory is required.");

            EnsureAuthenticated();

            _logger.LogDebug("Download file id={FileId} to={TargetDir}", fileId, targetDirectory);

            Directory.CreateDirectory(targetDirectory);

            var fileItem = await RunIomAsync(() =>
            {
                var item = _authenticator.Innovator.newItem("File", "get");
                item.setID(fileId);
                item.setAttribute("select", "id,filename");
                return item.apply();
            }, ct);

            CheckIomError(fileItem, "File get");

            var fileName = fileItem.getProperty("filename", fileId);
            var targetPath = Path.Combine(targetDirectory, fileName);

            await RunIomAsync(() =>
            {
                fileItem.fetchFileProperty("file_body", targetPath, FetchFileMode.Normal);
                return true; // Task.Run requires a return value
            }, ct);

            _logger.LogInformation("Downloaded file {FileId} to {TargetPath}", fileId, targetPath);
            return targetPath;
        }

        // ---- IArasCadClient - workflow context ----------------------------------

        public async Task<CadOperationContext> GetCadOperationContextAsync(
            string cadId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required.");

            EnsureAuthenticated();
            var currentUserId = _authenticator.Innovator.getUserID();
            var currentAssignmentIds = await GetCurrentUserAssignmentIdsAsync(currentUserId, ct);

            _logger.LogDebug("GetCadOperationContext cadId={CadId} userId={UserId}", cadId, currentUserId);

            // 1. Get CAD with lock and modified_on info
            var cadItem = await RunIomAsync(() =>
            {
                var getItem = _authenticator.Innovator.newItem("CAD", "get");
                getItem.setID(cadId);
                getItem.setAttribute("select", CadSelectFields.CadFull + ",modified_on,locked_by_id");
                return getItem.apply();
            }, ct);

            CheckIomError(cadItem, "CAD get for operation context");

            var cad = MapCadFromItem(cadItem);
            var modifiedOn = cadItem.getProperty("modified_on", null);
            var lockedById = cadItem.getProperty("locked_by_id", "");
            var lockOwnerName = cadItem.getProperty("locked_by_id\\keyed_name",
                cadItem.getProperty("locked_by_id", ""));

            // 2. Find active workflow process
            var activeWf = await FindActiveWorkflowProcessAsync(cadId, ct);

            // 3. Find active task for current user
            CadWorkflowTask task = null;
            if (activeWf != null)
            {
                task = await FindUserTaskAsync(activeWf, currentAssignmentIds, ct);
            }

            // 4. Calculate available actions
            var actions = BuildAvailableActions(cad, activeWf, task);

            return new CadOperationContext(
                cadId: cad.Id,
                cadNumber: cad.CadNumber,
                revision: cad.Revision,
                generation: cad.Generation,
                cadState: cad.State,
                modifiedOn: modifiedOn,
                hasNativeFile: cad.HasNativeFile,
                isLocked: cad.IsLocked,
                lockOwnerId: string.IsNullOrWhiteSpace(lockedById) ? null : lockedById,
                lockOwnerName: string.IsNullOrWhiteSpace(lockOwnerName) ? null : lockOwnerName,
                activeTask: task,
                availableActions: actions);
        }

        public async Task<CadOperationContext> ExecuteCadBusinessActionAsync(
            ExecuteCadBusinessActionRequest request,
            CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required.");

            EnsureAuthenticated();
            _logger.LogDebug("ExecuteCadBusinessAction cadId={CadId} action={Action}",
                request.CadId, request.Action);

            // Refresh context before execution to detect stale state
            var freshContext = await GetCadOperationContextAsync(request.CadId, ct);

            if (request.ExpectedModifiedOn != null
                && freshContext.ModifiedOn != request.ExpectedModifiedOn)
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowStaleContext,
                    "CAD was modified in Aras since the last refresh. Refresh and try again.");
            }

            switch (request.Action)
            {
                case CadBusinessActionKind.StartDetailedDesign:
                    await ExecuteStartDetailedDesignAsync(request, freshContext, ct);
                    break;

                case CadBusinessActionKind.SubmitForReview:
                    await ExecuteSubmitForReviewAsync(request, freshContext, ct);
                    break;

                case CadBusinessActionKind.Approve:
                    await ExecuteApproveCadReviewAsync(request, freshContext, ct);
                    break;
                case CadBusinessActionKind.RequestRework:
                    await ExecuteRequestCadReworkAsync(request, freshContext, ct);
                    break;

                case CadBusinessActionKind.Withdraw:
                    throw new ArasOperationException(
                        ArasErrorCode.WorkflowActionNotAvailable,
                        "Withdraw is not available: GATE-W evidence is pending. No server method has been verified for this action.");

                default:
                    throw new ArasOperationException(
                        ArasErrorCode.WorkflowActionNotAvailable,
                        $"Action '{request.Action}' is not supported by the workflow execution path.");
            }

            return await GetCadOperationContextAsync(request.CadId, ct);
        }

        private CadBusinessAction ResolveFreshAction(
            CadOperationContext context,
            ExecuteCadBusinessActionRequest request)
        {
            var candidates = (context?.AvailableActions ?? Array.Empty<CadBusinessAction>())
                .Where(a => a != null && a.IsAvailable && a.Kind == request.Action)
                .ToList();

            if (candidates.Count == 0)
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    $"Action '{request.Action}' is not available for the current CAD state.");
            }

            if (!string.IsNullOrWhiteSpace(request.WorkflowAssignmentId)
                || !string.IsNullOrWhiteSpace(request.WorkflowPathId))
            {
                var exact = candidates.FirstOrDefault(a =>
                    (string.IsNullOrWhiteSpace(request.WorkflowAssignmentId)
                        || string.Equals(a.WorkflowTaskId, request.WorkflowAssignmentId, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(request.WorkflowPathId)
                        || string.Equals(a.WorkflowPathId, request.WorkflowPathId, StringComparison.OrdinalIgnoreCase)));

                if (exact != null)
                    return exact;
            }

            if (candidates.Count == 1)
                return candidates[0];

            if (request.Action == CadBusinessActionKind.SubmitForReview
                && context?.ActiveTask != null)
            {
                var openPaths = (context.ActiveTask.AvailablePaths ?? Array.Empty<CadWorkflowPath>())
                    .Where(p => p != null && !p.IsComplete && !string.IsNullOrWhiteSpace(p.Id))
                    .ToList();

                if (openPaths.Count == 1)
                {
                    return new CadBusinessAction(
                        CadBusinessActionKind.SubmitForReview,
                        "Submit for Review",
                        true,
                        null,
                        true,
                        context.ActiveTask.AssignmentId,
                        openPaths[0].Id);
                }
            }

            throw new ArasOperationException(
                ArasErrorCode.WorkflowStaleContext,
                $"Multiple '{request.Action}' actions are available. Refresh the workflow context and retry.");
        }

        // ---- Workflow private helpers -------------------------------------------

        private async Task<Item> FindActiveWorkflowProcessAsync(string cadId, CancellationToken ct)
        {
            return await RunIomAsync(() =>
            {
                var wfItem = _authenticator.Innovator.newItem("Workflow Process", "get");
                wfItem.setProperty("source_id", cadId);
                wfItem.setAttribute("select", "id,name,state,started_on,ended_on");
                var result = wfItem.apply();

                if (result == null || result.isError() || result.getItemCount() == 0)
                    return null;

                // Find the first active (not cancelled, not completed) process
                for (var i = 0; i < result.getItemCount(); i++)
                {
                    var proc = result.getItemByIndex(i);
                    var state = proc.getProperty("state", "");
                    if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
                        return proc;
                }

                return null;
            }, ct);
        }

        private async Task<CadWorkflowTask> FindUserTaskAsync(Item workflowProcess, ISet<string> currentAssignmentIds, CancellationToken ct)
        {
            return await RunIomAsync(() =>
            {
                var wfId = workflowProcess.getProperty("id", "");

                // Get activities for this workflow process
                var activityItem = _authenticator.Innovator.newItem("Activity", "get");
                activityItem.setProperty("workflow_process_id", wfId);
                activityItem.setAttribute("select", "id,name,state,started_on,ended_on");
                var activitiesResult = activityItem.apply();

                if (activitiesResult == null || activitiesResult.isError())
                    return null;

                for (var i = 0; i < activitiesResult.getItemCount(); i++)
                {
                    var activity = activitiesResult.getItemByIndex(i);
                    var activityState = activity.getProperty("state", "");
                    var activityEndedOn = activity.getProperty("ended_on", "");

                    // Skip completed activities
                    if (!string.IsNullOrWhiteSpace(activityEndedOn))
                        continue;

                    if (!string.Equals(activityState, "Active", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(activityState, "Pending", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var activityId = activity.getProperty("id", "");
                    var activityName = activity.getProperty("name", "");

                    // Get assignments for this activity
                    var assignItem = _authenticator.Innovator.newItem("Activity Assignment", "get");
                    assignItem.setProperty("source_id", activityId);
                    assignItem.setAttribute("select",
                        "id,name,state,is_closed,completed_on,related_id,related_id\\keyed_name,swm_paths");
                    var assignResult = assignItem.apply();

                    if (assignResult == null || assignResult.isError())
                        continue;

                    for (var j = 0; j < assignResult.getItemCount(); j++)
                    {
                        var assignment = assignResult.getItemByIndex(j);
                        var isClosed = assignment.getProperty("is_closed", "0");
                        var completedOn = assignment.getProperty("completed_on", "");

                        // Skip closed or completed assignments
                        if (string.Equals(isClosed, "1", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrWhiteSpace(completedOn))
                            continue;

                        var assigneeId = assignment.getProperty("related_id", "");
                        if (string.IsNullOrWhiteSpace(assigneeId)
                            || currentAssignmentIds == null
                            || !currentAssignmentIds.Contains(assigneeId))
                            continue;

                        var assigneeName = assignment.getProperty("related_id\\keyed_name", assigneeId);

                        // Get available workflow paths for this assignment
                        var paths = new List<CadWorkflowPath>();
                        var pathsItem = _authenticator.Innovator.newItem("Workflow Process Path", "get");
                        pathsItem.setProperty("source_id", activityId);
                        pathsItem.setAttribute("select", "id,name,is_closed");
                        var pathsResult = pathsItem.apply();

                        if (pathsResult != null && !pathsResult.isError())
                        {
                            for (var k = 0; k < pathsResult.getItemCount(); k++)
                            {
                                var path = pathsResult.getItemByIndex(k);
                                var isPathClosed = path.getProperty("is_closed", "0");

                                paths.Add(new CadWorkflowPath(
                                    path.getProperty("id", ""),
                                    path.getProperty("name", ""),
                                    string.Equals(isPathClosed, "1", StringComparison.OrdinalIgnoreCase)));
                            }
                        }

                        var assignmentId = assignment.getProperty("id", "");
                        if (string.IsNullOrWhiteSpace(assignmentId))
                            continue;

                        return new CadWorkflowTask(
                            assignmentId: assignmentId,
                            activityId: activityId,
                            activityName: activityName,
                            workflowProcessId: wfId,
                            workflowProcessState: workflowProcess.getProperty("state", ""),
                            assigneeName: assigneeName,
                            availablePaths: paths.AsReadOnly());
                    }
                }

                return null;
            }, ct);
        }

        private async Task<HashSet<string>> GetCurrentUserAssignmentIdsAsync(string currentUserId, CancellationToken ct)
        {
            return await RunIomAsync(() =>
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(currentUserId))
                    return ids;

                ids.Add(currentUserId);

                var aliasIdentityId = GetAliasIdentityId(currentUserId);
                if (string.IsNullOrWhiteSpace(aliasIdentityId))
                    return ids;

                ids.Add(aliasIdentityId);

                var pending = new Queue<string>();
                pending.Enqueue(aliasIdentityId);

                while (pending.Count > 0)
                {
                    var relatedIdentityId = pending.Dequeue();
                    var memberItem = _authenticator.Innovator.newItem("Member", "get");
                    memberItem.setProperty("related_id", relatedIdentityId);
                    memberItem.setAttribute("select", "source_id");
                    var memberResult = memberItem.apply();

                    if (memberResult == null || memberResult.isError())
                        continue;

                    for (var i = 0; i < memberResult.getItemCount(); i++)
                    {
                        var member = memberResult.getItemByIndex(i);
                        var sourceId = member.getProperty("source_id", "");
                        if (string.IsNullOrWhiteSpace(sourceId))
                            continue;

                        if (ids.Add(sourceId))
                            pending.Enqueue(sourceId);
                    }
                }

                return ids;
            }, ct);
        }

        private string GetAliasIdentityId(string currentUserId)
        {
            var aliasItem = _authenticator.Innovator.newItem("Alias", "get");
            aliasItem.setProperty("source_id", currentUserId);
            aliasItem.setAttribute("select", "related_id");
            var aliasResult = aliasItem.apply();

            if (aliasResult == null || aliasResult.isError())
                return null;

            if (aliasResult.getItemCount() > 0)
                return aliasResult.getItemByIndex(0).getProperty("related_id", "");

            return aliasResult.getProperty("related_id", "");
        }

        private List<CadBusinessAction> BuildAvailableActions(
            CadSummary cad,
            Item workflowProcess,
            CadWorkflowTask task)
        {
            var actions = new List<CadBusinessAction>();

            // File actions based on lifecycle policy and lock state
            var canCheckout = CadLifecyclePolicy.CanCheckout(cad?.State)
                && cad != null && !cad.IsLocked;

            actions.Add(new CadBusinessAction(
                CadBusinessActionKind.Checkout,
                "Checkout",
                canCheckout,
                canCheckout ? null : GetCheckoutUnavailableReason(cad),
                false,
                null,
                null));

            actions.Add(new CadBusinessAction(
                CadBusinessActionKind.Checkin,
                "Check-in",
                cad != null && !string.IsNullOrWhiteSpace(cad.LockedBy),
                cad != null && !string.IsNullOrWhiteSpace(cad.LockedBy) ? null : "No active checkout.",
                false,
                null,
                null));

    if (task == null && cad != null && CadLifecyclePolicy.CanStartDetailedDesign(cad.State))
    {
        actions.Add(new CadBusinessAction(
            CadBusinessActionKind.StartDetailedDesign,
            "Start Detailed Design",
            true,
            null,
            true,
            null,
            null));
    }

    // Submit for Review when live submit task exists in detailed design flow
    if (task == null && cad != null && CadLifecyclePolicy.CanSubmitForReview(cad.State))
    {
        actions.Add(new CadBusinessAction(
            CadBusinessActionKind.SubmitForReview,
            "Submit for Review",
            true,
            null,
            false,
            null,
            null));
    }

    // Workflow actions
    if (task != null && workflowProcess != null)
    {
        foreach (var path in task.AvailablePaths)
        {
            if (path.IsComplete)
                continue;

            var actionKind = _actionMapper.Map(task.ActivityName, path.Name);

            if (actionKind == null)
            {
                _logger.LogWarning(
                    "Unrecognized workflow path: activity={Activity} path={Path}",
                    task.ActivityName, path.Name);
                continue;
            }

            var label = actionKind.Value switch
            {
                CadBusinessActionKind.SubmitForReview => "Submit for Review",
                CadBusinessActionKind.Approve => "Approve",
                CadBusinessActionKind.RequestRework => "Request Rework",
                _ => path.Name
            };

            actions.Add(new CadBusinessAction(
                actionKind.Value,
                label,
                true,
                null,
                true,
                task.AssignmentId,
                path.Id));
        }
    }

    return actions;
        }

        private async Task ExecuteStartDetailedDesignAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanStartDetailedDesign(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    LifecycleDisplayText.GetBusinessActionBlockedMessage(CadBusinessActionKind.StartDetailedDesign, context.CadState));
            }

            var result = await RunIomAsync(() =>
            {
                var methodItem = _authenticator.Innovator.newItem("Method", StartDetailedDesignMethodName);
                methodItem.setProperty("cad_id", request.CadId);
                methodItem.setProperty("comment", "Start Detailed Design");
                return methodItem.apply();
            }, ct);

            CheckIomError(result, StartDetailedDesignMethodName);
        }

        private async Task ExecuteSubmitForReviewAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanSubmitForReview(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    LifecycleDisplayText.GetBusinessActionBlockedMessage(CadBusinessActionKind.SubmitForReview, context.CadState));
            }

            var result = await RunIomAsync(() =>
            {
                var methodItem = _authenticator.Innovator.newItem("Method", SubmitCadForReviewMethodName);
                methodItem.setProperty("cad_id", request.CadId);
                methodItem.setProperty("comment", request.Comment ?? "Submit for Review");
                return methodItem.apply();
            }, ct);

            CheckIomError(result, SubmitCadForReviewMethodName);
        }

        private async Task ExecuteApproveCadReviewAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanApproveReview(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    LifecycleDisplayText.GetBusinessActionBlockedMessage(CadBusinessActionKind.Approve, context.CadState));
            }

            var result = await RunIomAsync(() =>
            {
                var methodItem = _authenticator.Innovator.newItem("Method", ApproveCadReviewMethodName);
                methodItem.setProperty("cad_id", request.CadId);
                methodItem.setProperty("comment", request.Comment ?? "Approve CAD Review");
                return methodItem.apply();
            }, ct);

            CheckIomError(result, ApproveCadReviewMethodName);
        }

        private async Task ExecuteRequestCadReworkAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanRequestRework(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    LifecycleDisplayText.GetBusinessActionBlockedMessage(CadBusinessActionKind.RequestRework, context.CadState));
            }

            var result = await RunIomAsync(() =>
            {
                var methodItem = _authenticator.Innovator.newItem("Method", RequestCadReworkMethodName);
                methodItem.setProperty("cad_id", request.CadId);
                methodItem.setProperty("comment", request.Comment ?? "Request CAD Rework");
                return methodItem.apply();
            }, ct);

            CheckIomError(result, RequestCadReworkMethodName);
        }

        private async Task EvaluateActivityAsync(
            string assignmentId,
            string pathId,
            string comment,
            CancellationToken ct)
        {
            await RunIomAsync(() =>
            {
                var evalItem = _authenticator.Innovator.newItem("Activity Assignment", "EvaluateActivity");
                evalItem.setID(assignmentId);

                if (!string.IsNullOrWhiteSpace(comment))
                    evalItem.setProperty("comments", comment);

                // Set the selected path
                var pathItem = evalItem.createRelationship("Workflow Process Path", "set");
                pathItem.setID(pathId);

                var result = evalItem.apply();

                if (result == null || result.isError())
                {
                    var errMsg = result?.getErrorString() ?? "EvaluateActivity returned null";
                    throw new ArasOperationException(
                        ArasErrorCode.WorkflowActionNotAvailable,
                        $"Workflow evaluation failed: {errMsg}");
                }

                return true;
            }, ct);
        }

        private static string GetCheckoutUnavailableReason(CadSummary cad)
        {
            if (cad == null || string.IsNullOrWhiteSpace(cad.Id))
                return "No CAD selected.";

            if (cad.IsLocked)
                return "CAD is locked by another user.";

            return LifecycleDisplayText.GetCheckoutBlockedMessage(cad.State);
        }

        private void EnsureAuthenticated()
        {
            if (_authenticator?.Innovator == null)
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "Not authenticated. Call LoginAsync first.");
        }

        private static void EnsureCadCanBeCheckedOut(CadSummary cad)
        {
            if (cad == null || string.IsNullOrWhiteSpace(cad.Id))
                throw new ArasOperationException(ArasErrorCode.CadNotFound, "CAD was not found.");

            if (cad.IsLocked)
                throw new ArasOperationException(ArasErrorCode.CadLocked, "CAD is already locked.");

            if (!CadLifecyclePolicy.CanCheckout(cad.State))
            {
                throw new ArasOperationException(
                    ArasErrorCode.CadReleasedReadOnly,
                    LifecycleDisplayText.GetCheckoutBlockedMessage(cad.State),
                    details: new Dictionary<string, string>
                    {
                        { "cad_id", cad.Id },
                        { "state", cad.State ?? string.Empty }
                    });
            }
        }

        /// <summary>
        /// Runs a synchronous IOM operation on a background thread so the
        /// caller's async context is not blocked.
        /// </summary>
        private static async Task<T> RunIomAsync<T>(Func<T> func, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return await Task.Run(func, ct);
        }

        /// <summary>
        /// Check an IOM Item for error and throw the appropriate
        /// <see cref="ArasOperationException"/>.
        /// </summary>
        private static void CheckIomError(Item item, string operation)
        {
            if (item == null)
                throw new ArasOperationException(
                    ArasErrorCode.UnexpectedServerError,
                    $"IOM operation '{operation}' returned null.");

            if (!item.isError())
                return;

            var errorMsg = item.getErrorString() ?? "Unknown IOM error";

            // Map known error prefixes to typed codes
            ArasErrorCode code;
            if (errorMsg.StartsWith("PART_NOT_FOUND", StringComparison.Ordinal))
                code = ArasErrorCode.PartNotFound;
            else if (errorMsg.StartsWith("VALIDATION_FAILED", StringComparison.Ordinal))
                code = ArasErrorCode.ValidationFailed;
            else if (errorMsg.StartsWith("CAD_NOT_FOUND", StringComparison.Ordinal))
                code = ArasErrorCode.CadNotFound;
            else if (errorMsg.StartsWith("CAD_CREATE_FAILED", StringComparison.Ordinal))
                code = ArasErrorCode.CadAlreadyExists;
            else if (errorMsg.StartsWith("CAD_LOCKED", StringComparison.Ordinal))
                code = ArasErrorCode.CadLocked;
            else if (errorMsg.StartsWith("CHECKIN_UPDATE_FAILED", StringComparison.Ordinal))
                code = ArasErrorCode.ValidationFailed;
            else if (errorMsg.StartsWith("UNLOCK_FAILED", StringComparison.Ordinal))
                code = ArasErrorCode.CadLocked;
            else
                code = ArasErrorCode.UnexpectedServerError;

            throw new ArasOperationException(code, $"IOM '{operation}' failed: {errorMsg}");
        }

        /// <summary>
        /// Map an IOM Item representing a CAD record to a <see cref="CadSummary"/>.
        /// </summary>
        private static CadSummary MapCadFromItem(Item item)
        {
            if (item == null || item.isError())
                return null;

            var lockedById = item.getProperty("locked_by_id", "");

            return new CadSummary
            {
                Id = item.getProperty("id", ""),
                CadNumber = item.getProperty("item_number", ""),
                Classification = item.getProperty("classification", ""),
                Revision = item.getProperty("major_rev", ""),
                State = item.getProperty("state", ""),
                Generation = ParseInt(item.getProperty("generation", "0")),
                NativeFileId = item.getProperty("native_file", ""),
                HasNativeFile = !string.IsNullOrWhiteSpace(item.getProperty("native_file", "")),
                IsLocked = !string.IsNullOrWhiteSpace(lockedById),
                LockedBy = lockedById
            };
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private static string EscapeAml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _authenticator?.Dispose();
                _partSearch = null;
                _disposed = true;
            }
        }
    }
}
