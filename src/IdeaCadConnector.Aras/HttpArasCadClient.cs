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
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Pure HTTP/REST implementation of IArasCadClient. No dependency on Aras.IOM.
    /// The client is intentionally thin: all business rules live in Aras Innovator
    /// server methods (idea_EnsurePrimaryIronCadPartCad, idea_CommitCadCheckin).
    /// </summary>
    public sealed class HttpArasCadClient : IArasCadClient, IDisposable
    {

        private const string EnsurePrimaryCadMethodName = "idea_EnsurePrimaryIronCadPartCad";
        private const string CommitCadCheckinMethodName = "idea_CommitCadCheckin";
        private const string StartDetailedDesignMethodName = "idea_StartDetailedDesign";

        private readonly ArasClientOptions _options;
        private readonly ILogger<HttpArasCadClient> _logger;
        private readonly WorkflowActionMapper _actionMapper;
        private ArasHttpClient _http;
        private ArasAmlClient _aml;
        private PartSearchClient _partSearch;
        private VaultClient _vault;
        private string _accessToken;
        private string _tokenType;
        private string _userId;
        private HashSet<string> _assignmentIds;
        private bool _disposed;

        public HttpArasCadClient(ArasClientOptions options, ILogger<HttpArasCadClient> logger = null)
            : this(options, logger, null)
        {
        }

        public HttpArasCadClient(ArasClientOptions options, ILogger<HttpArasCadClient> logger, WorkflowActionMapper actionMapper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpArasCadClient>.Instance;
            _actionMapper = actionMapper ?? WorkflowActionMapper.CreateDefault();
        }

        public async Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            _http?.Dispose();
            _http = new ArasHttpClient(_options.BaseUri, _options.Timeout);

            var formFields = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", "IOMApp" },
                { "scope", "Innovator" },
                { "database", request.Database ?? _options.Database },
                { "username", request.UserName },
                { "password", request.Password }
            };

            using var content = new FormUrlEncodedContent(formFields);
            var response = await _http.PostMultipartAsync("oauthserver/connect/token", content, ct).ConfigureAwait(false);

            _accessToken = response["access_token"]?.Value<string>();
            _tokenType = response["token_type"]?.Value<string>() ?? "Bearer";

            if (string.IsNullOrWhiteSpace(_accessToken))
                throw new ArasOperationException(ArasErrorCode.AuthInvalid, "OAuth login did not return an access token.");

            _http.SetBearerToken(_accessToken, _tokenType);
            _aml = new ArasAmlClient(_http, request.Database ?? _options.Database);

            // Set up OData Part search client reusing a dedicated HttpClient
            var searchHttp = new HttpClient
            {
                BaseAddress = _options.BaseUri,
                Timeout = _options.Timeout
            };
            searchHttp.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(_tokenType, _accessToken);

            _partSearch = new PartSearchClient(searchHttp, _options);
            _vault = new VaultClient(_http, _options);

            // Look up the current user's Aras user ID and all assignment-relevant identities
            string userId = null;
            try
            {
                var userAml = "<Item type=\"User\" action=\"get\" select=\"id,login_name\">" +
                              "  <login_name condition=\"eq\">" + EscapeAml(request.UserName) + "</login_name>" +
                              "</Item>";
                var userResult = await _aml.ApplyAmlAsync(userAml, "get", "User", null, ct);
                userId = userResult?["id"]?.Value<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to look up user ID during login.");
            }

            _userId = userId ?? request.UserName;
            try
            {
                _assignmentIds = await ResolveAssignmentIdsAsync(_userId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve assignment identities during login. Falling back to user ID only.");
                _assignmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(_userId))
                    _assignmentIds.Add(_userId);
            }

            return new ArasLoginResult
            {
                SessionToken = _accessToken,
                TokenType = _tokenType,
                UserId = _userId,
                UserName = request.UserName,
                DisplayName = request.UserName,
                Database = request.Database ?? _options.Database
            };
        }

        private string GetCurrentUserId()
        {
            if (string.IsNullOrWhiteSpace(_userId))
            {
                // Try to recover it if possible, or throw
                throw new ArasOperationException(ArasErrorCode.AuthInvalid, "User ID not available.");
            }
            return _userId;
        }

        private ISet<string> GetCurrentAssignmentIds()
        {
            if (_assignmentIds == null || _assignmentIds.Count == 0)
            {
                var fallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(_userId))
                    fallback.Add(_userId);
                return fallback;
            }

            return _assignmentIds;
        }

        private async Task<HashSet<string>> ResolveAssignmentIdsAsync(string userId, CancellationToken ct)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(userId))
                return ids;

            ids.Add(userId);

            var aliasAml = "<Item type=\"Alias\" action=\"get\" select=\"related_id\">" +
                           "  <source_id condition=\"eq\">" + EscapeAml(userId) + "</source_id>" +
                           "</Item>";

            JObject aliasResult;
            try
            {
                aliasResult = await _aml.ApplyAmlAsync(aliasAml, "get", "Alias", null, ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return ids;
            }

            foreach (var aliasItem in EnumerateItems(aliasResult))
            {
                var aliasIdentityId = aliasItem["related_id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(aliasIdentityId) || !ids.Add(aliasIdentityId))
                    continue;

                var pending = new Queue<string>();
                pending.Enqueue(aliasIdentityId);

                while (pending.Count > 0)
                {
                    var relatedIdentityId = pending.Dequeue();
                    var memberAml = "<Item type=\"Member\" action=\"get\" select=\"source_id\">" +
                                    "  <related_id condition=\"eq\">" + EscapeAml(relatedIdentityId) + "</related_id>" +
                                    "</Item>";

                    JObject memberResult;
                    try
                    {
                        memberResult = await _aml.ApplyAmlAsync(memberAml, "get", "Member", null, ct).ConfigureAwait(false);
                    }
                    catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
                    {
                        continue;
                    }

                    foreach (var memberItem in EnumerateItems(memberResult))
                    {
                        var sourceId = memberItem["source_id"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(sourceId))
                            continue;

                        if (ids.Add(sourceId))
                            pending.Enqueue(sourceId);
                    }
                }
            }

            return ids;
        }

        public async Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
        {
            EnsureAuthenticated();
            var (items, totalCount) = await _partSearch.SearchAsync(request, ct).ConfigureAwait(false);
            items = await EnrichPartResultsWithLiveCadAsync(items, ct).ConfigureAwait(false);
            return new PartSearchResponse(items, totalCount);
        }

        public async Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
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

            var result = await _aml.ApplyMethodAsync(
                EnsurePrimaryCadMethodName,
                new Dictionary<string, string> { { "part_id", request.PartId } },
                ct).ConfigureAwait(false);

            return new CreateCadResult
            {
                Cad = MapCadFromToken(result)
            };
        }

        public async Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for checkout.");

            EnsureAuthenticated();

            var currentCadToken = await _aml.ApplyItemAsync(
                "CAD",
                request.CadId,
                "get",
                CadSelectFields.CadFull,
                ct).ConfigureAwait(false);

            var currentCad = MapCadFromToken(currentCadToken);
            EnsureCadCanBeCheckedOut(currentCad);

            var lockResult = await _aml.ApplyItemAsync(
                "CAD",
                request.CadId,
                "lock",
                CadSelectFields.CadFull,
                ct).ConfigureAwait(false);

            return new CadCheckoutResult
            {
                LockToken = request.CadId,
                Cad = MapCadFromToken(lockResult),
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

            var cad = await _aml.ApplyItemAsync(
                "CAD",
                request.CadId,
                "get",
                CadSelectFields.CadFull,
                ct).ConfigureAwait(false);

            return new CadCheckoutResult
            {
                LockToken = null,
                Cad = MapCadFromToken(cad),
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

            var fileId = await _vault.UploadFileAsync(request.FilePath, request.FileName, ct).ConfigureAwait(false);
            var fileInfo = new FileInfo(request.FilePath);

            return new FileUploadResult
            {
                UploadedFileId = fileId,
                FileName = request.FileName ?? Path.GetFileName(request.FilePath),
                SizeBytes = fileInfo.Length
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

            var parameters = new Dictionary<string, string>
            {
                { "cad_id", request.CadId },
                { "uploaded_file_id", request.UploadedFileId }
            };

            if (!string.IsNullOrWhiteSpace(request.Comment))
                parameters.Add("comment", request.Comment);

            var result = await _aml.ApplyMethodAsync(CommitCadCheckinMethodName, parameters, ct).ConfigureAwait(false);

            return new CadCheckinResult
            {
                Success = true,
                Cad = MapCadFromToken(result),
                Message = "Check-in completed successfully."
            };
        }

        public async Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required for cancel checkout.");

            EnsureAuthenticated();

            await _aml.ApplyItemAsync("CAD", request.CadId, "unlock", CadSelectFields.CadFull, ct).ConfigureAwait(false);

            return new CancelCheckoutResult
            {
                Success = true,
                Message = "Checkout cancelled successfully."
            };
        }

        /// <summary>
        /// Convenience download not on the IArasCadClient interface.
        /// </summary>
        public async Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
        {
            EnsureAuthenticated();
            return await _vault.DownloadFileAsync(fileId, targetDirectory, ct).ConfigureAwait(false);
        }

        public async Task<CadOperationContext> GetCadOperationContextAsync(
            string cadId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required.");

            EnsureAuthenticated();
            
            // Get current user assignment identities (stored during login)
            var currentAssignmentIds = GetCurrentAssignmentIds();

            _logger.LogDebug("GetCadOperationContext cadId={CadId} userId={UserId}", cadId, GetCurrentUserId());

            // 1. Get CAD with extended fields
            JToken cadToken;
            try
            {
                cadToken = await _aml.ApplyItemAsync("CAD", cadId, "get",
                    CadSelectFields.CadFull + ",modified_on,locked_by_id", ct);
            }
            catch (Exception)
            {
                throw;
            }

            var cad = MapCadFromToken(cadToken);
            var modifiedOn = cadToken["modified_on"]?.Value<string>();
            var lockedById = cadToken["locked_by_id"]?.Value<string>();
            var lockOwnerName = cadToken["locked_by_id\\keyed_name"]?.Value<string>()
                ?? lockedById;

            JObject activeWf = null;
            CadWorkflowTask task = null;

            // Business rule: while CAD is still in 'Khoi tao', the guided action is
            // "Start Detailed Design". We do not need workflow discovery to light up
            // that button, and skipping workflow lookup here avoids pulling unrelated
            // historical workflow context into the initial-design screen.
            if (!CadLifecyclePolicy.CanStartDetailedDesign(cad?.State))
            {
                activeWf = await FindActiveWorkflowProcessHttpAsync(cadId, ct);

                if (activeWf != null)
                {
                    task = await FindUserTaskHttpAsync(activeWf, currentAssignmentIds, ct);
                }
            }

            // 4. Build actions
            var actions = BuildAvailableActionsHttp(cad, activeWf, task);

            return new CadOperationContext(
                cadId: cad?.Id ?? cadId,
                cadNumber: cad?.CadNumber ?? "",
                revision: cad?.Revision ?? "",
                generation: cad?.Generation ?? 0,
                cadState: cad?.State ?? "",
                modifiedOn: modifiedOn,
                hasNativeFile: cad?.HasNativeFile ?? false,
                isLocked: cad?.IsLocked ?? false,
                lockOwnerId: lockedById,
                lockOwnerName: lockOwnerName,
                activeTask: task,
                availableActions: actions);
        }

        public async Task<CadOperationContext> ExecuteCadBusinessActionAsync(
            ExecuteCadBusinessActionRequest request, CancellationToken ct)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.CadId))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "CadId is required.");

            EnsureAuthenticated();

            // Refresh context for stale check
            var freshContext = await GetCadOperationContextAsync(request.CadId, ct);

            if (request.ExpectedModifiedOn != null
                && freshContext.ModifiedOn != request.ExpectedModifiedOn)
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowStaleContext,
                    "CAD was modified in Aras since the last refresh. Refresh and try again.");
            }

            var freshAction = ResolveFreshAction(freshContext, request);

            switch (request.Action)
            {
                case CadBusinessActionKind.StartDetailedDesign:
                    await ExecuteStartDetailedDesignHttpAsync(request, freshContext, ct);
                    break;

                case CadBusinessActionKind.SubmitForReview:
                    await ExecuteSubmitForReviewHttpAsync(request, freshContext, freshAction, ct);
                    break;
                case CadBusinessActionKind.Approve:
                case CadBusinessActionKind.RequestRework:
                    await ExecuteVoteActionHttpAsync(request, freshAction, ct);
                    break;
                default:
                    throw new ArasOperationException(
                        ArasErrorCode.WorkflowActionNotAvailable,
                        $"Action '{request.Action}' is not supported.");
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

        // ---- HTTP workflow helpers ---------------------------------------------

        private async Task<JObject> FindActiveWorkflowProcessHttpAsync(string cadId, CancellationToken ct)
        {
            var wfAml = "<Item type=\"Workflow Process\" action=\"get\" select=\"id,name,state,started_on,ended_on\">" +
                        "  <source_id condition=\"eq\">" + EscapeAml(cadId) + "</source_id>" +
                        "</Item>";

            JObject result;
            try
            {
                result = await _aml.ApplyAmlAsync(wfAml, "get", "Workflow Process", cadId, ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return null;
            }

            foreach (var process in EnumerateItems(result))
            {
                var state = process["state"]?.Value<string>();
                if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
                    return process;
            }

            return null;
        }

        private async Task<CadWorkflowTask> FindUserTaskHttpAsync(
            JObject workflowProcess, ISet<string> currentAssignmentIds, CancellationToken ct)
        {
            var wfId = workflowProcess["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(wfId))
                return null;

            // Get activities for this workflow process
            var actAml = "<Item type=\"Activity\" action=\"get\" select=\"id,name,state,started_on,ended_on,workflow_process_id\">" +
                         "  <workflow_process_id condition=\"eq\">" + EscapeAml(wfId) + "</workflow_process_id>" +
                         "</Item>";

            var actResult = await _aml.ApplyAmlAsync(actAml, "get", "Activity", wfId, ct);
            if (actResult == null)
                return null;

            foreach (var activity in EnumerateItems(actResult))
            {
                var activityId = activity["id"]?.Value<string>();
                var activityName = activity["name"]?.Value<string>();
                var activityState = activity["state"]?.Value<string>();
                var endedOn = activity["ended_on"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(activityId))
                    continue;
                if (!string.IsNullOrWhiteSpace(endedOn))
                    continue;
                if (!string.Equals(activityState, "Active", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(activityState, "Pending", StringComparison.OrdinalIgnoreCase))
                    continue;

                var assignAml = "<Item type=\"Activity Assignment\" action=\"get\" " +
                                "select=\"id,name,state,is_closed,completed_on,related_id,related_id\\keyed_name,swm_paths\">" +
                                "  <source_id condition=\"eq\">" + EscapeAml(activityId) + "</source_id>" +
                                "</Item>";

                JObject assignResult;
                try
                {
                    assignResult = await _aml.ApplyAmlAsync(assignAml, "get", "Activity Assignment", activityId, ct);
                }
                catch (ArasOperationException)
                {
                    continue;
                }
                if (assignResult == null)
                    continue;

                foreach (var assignment in EnumerateItems(assignResult))
                {
                    var assignId = assignment["id"]?.Value<string>();
                    var isClosed = assignment["is_closed"]?.Value<string>();
                    var completedOn = assignment["completed_on"]?.Value<string>();
                    var assigneeId = assignment["related_id"]?.Value<string>();
                    var assigneeName = assignment["related_id\\keyed_name"]?.Value<string>() ?? assigneeId;

                    if (string.IsNullOrWhiteSpace(assignId))
                        continue;
                    if (string.Equals(isClosed, "1", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrWhiteSpace(completedOn))
                        continue;
                    if (string.IsNullOrWhiteSpace(assigneeId)
                        || currentAssignmentIds == null
                        || !currentAssignmentIds.Contains(assigneeId))
                        continue;

                    var paths = new List<CadWorkflowPath>();
                    var pathAml = "<Item type=\"Workflow Process Path\" action=\"get\" select=\"id,name,is_closed\">" +
                                  "  <source_id condition=\"eq\">" + EscapeAml(activityId) + "</source_id>" +
                                  "</Item>";

                    JObject pathResult;
                    try
                    {
                        pathResult = await _aml.ApplyAmlAsync(pathAml, "get", "Workflow Process Path", activityId, ct);
                    }
                    catch (ArasOperationException)
                    {
                        pathResult = null;
                    }
                    foreach (var path in EnumerateItems(pathResult))
                    {
                        var pathId = path["id"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(pathId))
                            continue;

                        paths.Add(new CadWorkflowPath(
                            pathId,
                            path["name"]?.Value<string>() ?? "",
                            string.Equals(path["is_closed"]?.Value<string>(), "1", StringComparison.OrdinalIgnoreCase)));
                    }

                    return new CadWorkflowTask(
                        assignmentId: assignId,
                        activityId: activityId,
                        activityName: activityName,
                        workflowProcessId: wfId,
                        workflowProcessState: workflowProcess["state"]?.Value<string>() ?? "",
                        assigneeName: assigneeName,
                        availablePaths: paths.AsReadOnly());
                }
            }

            return null;
        }

        private List<CadBusinessAction> BuildAvailableActionsHttp(
            CadSummary cad, JObject workflowProcess, CadWorkflowTask task)
        {
            var actions = new List<CadBusinessAction>();

            var canCheckout = cad != null
                && CadLifecyclePolicy.CanCheckout(cad.State)
                && !cad.IsLocked;

            actions.Add(new CadBusinessAction(
                CadBusinessActionKind.Checkout, "Checkout",
                canCheckout,
                canCheckout ? null : GetCheckoutUnavailableReasonHttp(cad),
                false, null, null));

            actions.Add(new CadBusinessAction(
                CadBusinessActionKind.Checkin, "Check-in",
                cad != null && !string.IsNullOrWhiteSpace(cad.LockedBy),
                cad != null && !string.IsNullOrWhiteSpace(cad.LockedBy) ? null : "No active checkout.",
                false, null, null));

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

    // Submit for Review when detailed design live context allows it
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

    if (task != null && workflowProcess != null)
    {
        foreach (var path in task.AvailablePaths)
        {
            if (path.IsComplete)
                continue;

            var actionKind = _actionMapper.Map(task.ActivityName, path.Name);
            if (actionKind == null)
            {
                _logger.LogWarning("Unrecognized workflow path: activity={A} path={P}",
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
                actionKind.Value, label, true, null, true,
                task.AssignmentId, path.Id));
        }
    }

    return actions;
        }

        private async Task ExecuteStartDetailedDesignHttpAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanStartDetailedDesign(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    CadLifecyclePolicy.GetStartDetailedDesignBlockedMessage(context.CadState));
            }

            try
            {
                await _aml.ApplyMethodAsync(
                    StartDetailedDesignMethodName,
                    new Dictionary<string, string>
                    {
                        { "cad_id", request.CadId },
                        { "comment", "Start Detailed Design" }
                    },
                    ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex)
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    "Failed to move CAD to 'Thiet ke chi tiet': " + ex.Message);
            }
        }

        private async Task ExecuteSubmitForReviewHttpAsync(
            ExecuteCadBusinessActionRequest request,
            CadOperationContext context,
            CadBusinessAction freshAction,
            CancellationToken ct)
        {
            if (!CadLifecyclePolicy.CanSubmitForReview(context.CadState))
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    CadLifecyclePolicy.GetSubmitForReviewBlockedMessage(context.CadState));
            }

            var activeWf = await FindActiveWorkflowProcessHttpAsync(request.CadId, ct);

            if (activeWf != null)
            {
                if (string.IsNullOrWhiteSpace(freshAction?.WorkflowTaskId)
                    || string.IsNullOrWhiteSpace(freshAction?.WorkflowPathId))
                {
                    throw new ArasOperationException(ArasErrorCode.WorkflowActionNotAvailable,
                        "Active workflow found but no task or path specified.");
                }
                await EvaluateActivityHttpAsync(freshAction.WorkflowTaskId, freshAction.WorkflowPathId, request.Comment, ct);
            }
            else
            {
                // Initiate workflow
                var initAml = "<Item type=\"CAD\" action=\"startWorkflow\" id=\"" + EscapeAml(request.CadId) + "\" />";
                try
                {
                    await _aml.ApplyAmlAsync(initAml, "startWorkflow", "CAD", request.CadId, ct);
                }
                catch (ArasOperationException ex)
                {
                    throw new ArasOperationException(
                        ArasErrorCode.WorkflowActionNotAvailable,
                        "Failed to initiate workflow: " + ex.Message);
                }

                // Try auto-evaluate submit path
                var freshWf = await FindActiveWorkflowProcessHttpAsync(request.CadId, ct);
                if (freshWf != null)
                {
                    var freshContext = await GetCadOperationContextAsync(request.CadId, ct);
                    var submitAction = ResolveFreshAction(
                        freshContext,
                        new ExecuteCadBusinessActionRequest(
                            request.CadId,
                            CadBusinessActionKind.SubmitForReview,
                            null,
                            null,
                            null,
                            request.Comment));

                    if (!string.IsNullOrWhiteSpace(submitAction.WorkflowTaskId)
                        && !string.IsNullOrWhiteSpace(submitAction.WorkflowPathId))
                    {
                        await EvaluateActivityHttpAsync(submitAction.WorkflowTaskId, submitAction.WorkflowPathId, request.Comment, ct);
                    }
                }
            }
        }

        private async Task ExecuteVoteActionHttpAsync(
            ExecuteCadBusinessActionRequest request,
            CadBusinessAction freshAction,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(freshAction?.WorkflowTaskId))
                throw new ArasOperationException(ArasErrorCode.WorkflowActionNotAvailable,
                    "No workflow assignment found.");
            if (string.IsNullOrWhiteSpace(freshAction?.WorkflowPathId))
                throw new ArasOperationException(ArasErrorCode.WorkflowPathNotFound,
                    "No workflow path specified.");

            await EvaluateActivityHttpAsync(freshAction.WorkflowTaskId, freshAction.WorkflowPathId, request.Comment, ct);
        }

        private async Task EvaluateActivityHttpAsync(string assignmentId, string pathId, string comment, CancellationToken ct)
        {
            var pathAml = "<Item type=\"Workflow Process Path\" action=\"get\" id=\"" + EscapeAml(pathId) + "\" select=\"id,name\" />";

            var evalAml = "<Item type=\"Activity Assignment\" action=\"EvaluateActivity\" id=\"" + EscapeAml(assignmentId) + "\">" +
                          "  <comments>" + EscapeAml(comment ?? "") + "</comments>" +
                          "  <Relationships>" +
                          "    <Item type=\"Workflow Process Path\" action=\"set\" id=\"" + EscapeAml(pathId) + "\" />" +
                          "  </Relationships>" +
                          "</Item>";

            try
            {
                await _aml.ApplyAmlAsync(evalAml, "EvaluateActivity", "Activity Assignment", assignmentId, ct);
            }
            catch (ArasOperationException ex)
            {
                throw new ArasOperationException(
                    ArasErrorCode.WorkflowActionNotAvailable,
                    "Workflow evaluation failed: " + ex.Message);
            }
        }

        private static IEnumerable<JObject> EnumerateItems(JObject result)
        {
            if (result == null)
                yield break;

            if (result["Items"] is JArray items)
            {
                foreach (var token in items.OfType<JObject>())
                    yield return token;
                yield break;
            }

            if (result.Properties().Any())
                yield return result;
        }

        private async Task<IReadOnlyList<PartSearchResult>> EnrichPartResultsWithLiveCadAsync(
            IReadOnlyList<PartSearchResult> items,
            CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return items ?? Array.Empty<PartSearchResult>();

            var enriched = new List<PartSearchResult>(items.Count);
            foreach (var item in items)
            {
                if (item?.Part == null)
                {
                    enriched.Add(item);
                    continue;
                }

                var linkedCad = item.IronCadPartCad;
                if (linkedCad == null && !string.IsNullOrWhiteSpace(item.Part.Id))
                {
                    linkedCad = await ResolvePrimaryIronCadPartCadAsync(item.Part.Id, ct).ConfigureAwait(false);
                }

                enriched.Add(new PartSearchResult
                {
                    Part = item.Part,
                    IronCadPartCad = linkedCad
                });
            }

            return enriched;
        }

        private async Task<CadSummary> ResolvePrimaryIronCadPartCadAsync(string partId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partId))
                return null;

            var relAml =
                "<Item type=\"Part CAD\" action=\"get\" select=\"related_id\">" +
                "  <source_id condition=\"eq\">" + EscapeAml(partId) + "</source_id>" +
                "</Item>";

            JObject relResult;
            try
            {
                relResult = await _aml.ApplyAmlAsync(relAml, "get", "Part CAD", partId, ct).ConfigureAwait(false);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                return null;
            }
            foreach (var rel in EnumerateItems(relResult))
            {
                var cadId = rel["related_id"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(cadId))
                    continue;

                try
                {
                    var cadToken = await _aml.ApplyItemAsync("CAD", cadId, "get", CadSelectFields.CadFull, ct).ConfigureAwait(false);
                    var classification = cadToken["classification"]?.Value<string>();
                    var authoringTool = cadToken["authoring_tool"]?.Value<string>();

                    if (!string.Equals(classification, CadConstants.IronCadPartClassification, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.Equals(authoringTool, CadConstants.IronCadAuthoringTool, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return MapCadFromToken(cadToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve linked CAD {CadId} for part {PartId}.", cadId, partId);
                }
            }

            return null;
        }

        private static string GetCheckoutUnavailableReasonHttp(CadSummary cad)
        {
            if (cad == null || string.IsNullOrWhiteSpace(cad.Id))
                return "No CAD selected.";
            if (cad.IsLocked)
                return "CAD is locked by another user.";
            return CadLifecyclePolicy.GetCheckoutBlockedMessage(cad.State);
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
                _http?.Dispose();
                _partSearch = null;
                _vault = null;
                _disposed = true;
            }
        }

        private void EnsureAuthenticated()
        {
            if (_http == null || string.IsNullOrWhiteSpace(_accessToken))
                throw new ArasOperationException(ArasErrorCode.AuthInvalid, "Not authenticated. Call LoginAsync first.");
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
                    CadLifecyclePolicy.GetCheckoutBlockedMessage(cad.State),
                    details: new Dictionary<string, string>
                    {
                        { "cad_id", cad.Id },
                        { "state", cad.State ?? string.Empty }
                    });
            }
        }

        private static CadSummary MapCadFromToken(JToken token)
        {
            if (token == null)
                return null;

            var lockedById = token["locked_by_id"]?.Value<string>();

            return new CadSummary
            {
                Id = token["id"]?.Value<string>(),
                CadNumber = token["item_number"]?.Value<string>(),
                Classification = token["classification"]?.Value<string>(),
                Revision = token["major_rev"]?.Value<string>(),
                State = token["state"]?.Value<string>(),
                Generation = token["generation"]?.Value<int>() ?? 0,
                NativeFileId = token["native_file"]?.Value<string>(),
                HasNativeFile = !string.IsNullOrWhiteSpace(token["native_file"]?.Value<string>()),
                IsLocked = !string.IsNullOrWhiteSpace(lockedById),
                LockedBy = lockedById
            };
        }

    }
}
