using System.Collections.Generic;
using System.Reflection;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Workflow;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class ReviewerEnforcementTests
    {
        private sealed class RecordingDialogService : IWorkflowActionDialogService
        {
            public bool GatePendingShown { get; private set; }
            public bool WithdrawConfirmed { get; set; } = true;

            public CheckinReasonDialogResult ShowCheckinReason()
                => new CheckinReasonDialogResult();
            public SubmitForReviewDialogResult ShowSubmitForReview(string cadInfo, string partInfo)
                => new SubmitForReviewDialogResult { Confirmed = false };
            public ReviewDecisionDialogResult ShowReviewDecision(string submissionInfo, string gateNote)
                => new ReviewDecisionDialogResult { Confirmed = false };
            public bool ShowWithdrawConfirm(string submissionInfo) => WithdrawConfirmed;
            public bool ShowGatePending(string title, string message)
            {
                GatePendingShown = true;
                return false;
            }
            public bool ShowReviewerUnavailable(string title, string message) => false;
            public bool ConfirmSimple(string title, string message) => false;
        }

        private static PdmProjectsViewModel BuildViewModelWithGate(
            CadWorkflowGate gate, RecordingDialogService dialog)
        {
            return new PdmProjectsViewModel(
                new GuidanceRevisionService(),
                new CadLifecyclePolicy(),
                gate,
                dialog,
                PdmProjectsViewModel.CreateDefaultReleaseEligibility(new CadLifecyclePolicy(), gate));
        }

        private static void SetLiveContext(
            PdmProjectsViewModel vm,
            string cadId,
            string cadState,
            IReadOnlyList<CadBusinessAction> actions,
            string assigneeName = null,
            string lockOwnerName = null,
            string partState = null)
        {
            var task = assigneeName == null
                ? null
                : new CadWorkflowTask("assignment1", "activity1", "Activity", "wp1", "Active", assigneeName, null);
            var context = new CadOperationContext(
                cadId, "CAD-001", "A", 1, cadState, "2026-01-01", true, false, null, lockOwnerName,
                task, actions);
            typeof(PdmProjectsViewModel)
                .GetField("_cadOperationContext", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, context);
            typeof(PdmProjectsViewModel)
                .GetField("_liveCadId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, cadId);
            typeof(PdmProjectsViewModel)
                .GetField("_liveCadState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, cadState);
            typeof(PdmProjectsViewModel)
                .GetField("_livePartState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, partState);
        }

        private static CadBusinessAction Action(CadBusinessActionKind kind) =>
            new CadBusinessAction(kind, kind.ToString(), true, null, false, "task1", "path1");

        // T032: Approve/RequestRework are blocked unless the current user is the
        // authoritative assigned reviewer (ActiveTask.AssigneeName).
        [Theory]
        [InlineData("engineer1", "engineer1", true)]
        [InlineData("engineer1", "engineer2", false)]
        [InlineData("engineer1", null, false)]
        [InlineData(null, "engineer1", false)]
        public void IsCurrentUserAssignedReviewer_EnforcesAssignment(
            string currentUser, string assignee, bool expected)
        {
            MainViewModel.SharedUserName = currentUser;
            var vm = BuildViewModelWithGate(new CadWorkflowGate(), new RecordingDialogService());
            var task = assignee == null
                ? null
                : new CadWorkflowTask("a", "act", "Activity", "wp", "Active", assignee, null);
            var context = new CadOperationContext(
                "CAD1", "CAD-001", "A", 1, "In Review", "2026-01-01", true, false, null, null,
                task, System.Array.Empty<CadBusinessAction>());

            try
            {
                var result = vm.IsCurrentUserAssignedReviewer(context);
                Assert.Equal(expected, result);
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // T033: Withdraw is disabled while GATE-W is closed.
        [Fact]
        public void Withdraw_IsDisabledWhileGateClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) },
                    lockOwnerName: "engineer1");

                Assert.True(CadWorkflowGate.IsGated(CadBusinessActionKind.Withdraw));
                Assert.True(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));

                gate.OpenGate(CadBusinessActionKind.Withdraw);
                Assert.False(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
                // LockOwnerName is not an authoritative submission owner. The
                // current contract has no submitter field, so Withdraw remains
                // fail-closed even after GATE-W is opened.
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // F3: Withdraw is disabled when owner is missing (LockOwnerName is null),
        // even after the gate is opened.
        [Fact]
        public void Withdraw_DisabledWhenOwnerMissing()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) },
                    lockOwnerName: null);

                gate.OpenGate(CadBusinessActionKind.Withdraw);

                Assert.False(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // F3: Withdraw is disabled when the current user does not match the LockOwnerName.
        [Fact]
        public void Withdraw_DisabledWhenOwnerMismatch()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer2";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) },
                    lockOwnerName: "engineer1");

                gate.OpenGate(CadBusinessActionKind.Withdraw);

                Assert.False(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // F3: Withdraw is disabled when the gate is closed, even with correct owner.
        [Fact]
        public void Withdraw_DisabledWhenGateClosed_CorrectOwner()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) },
                    lockOwnerName: "engineer1");

                Assert.True(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // F5: Approve is blocked when the current user is not the assigned reviewer.
        [Fact]
        public void Approve_DisabledWhenNotAssignedReviewer()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "engineer2");

                gate.OpenGate(CadBusinessActionKind.Approve);
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // F5: Approve is enabled when the current user is the assigned reviewer
        // and the gate is open.
        [Fact]
        public void Approve_EnabledWhenAssignedReviewer()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "engineer1",
                    partState: "In Review");

                gate.OpenGate(CadBusinessActionKind.Approve);
                gate.OpenPartReleaseGate();
                gate.OpenReviewerAssignmentGate();
                Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Approve_DisabledWhenReviewerGateClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "engineer1",
                    partState: "In Review");

                gate.OpenGate(CadBusinessActionKind.Approve);
                gate.OpenPartReleaseGate();
                Assert.False(gate.IsReviewerAssignmentAvailable());
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void RequestRework_DisabledWhenReviewerGateClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                    assigneeName: "engineer1");

                gate.OpenGate(CadBusinessActionKind.RequestRework);
                Assert.False(gate.IsReviewerAssignmentAvailable());
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void RequestRework_EnabledWhenReviewerGateOpen()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                    assigneeName: "engineer1");

                gate.OpenGate(CadBusinessActionKind.RequestRework);
                gate.OpenReviewerAssignmentGate();
                Assert.True(gate.IsReviewerAssignmentAvailable());
                Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // Withdraw is always disabled because GATE-W and GATE-W-owner are both
        // closed, and no authoritative submission-owner field exists in the
        // current authority contract.
        [Fact]
        public void Withdraw_AlwaysDisabled()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) },
                    lockOwnerName: "engineer1");

                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
                gate.OpenGate(CadBusinessActionKind.Withdraw);
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
                gate.OpenReviewerAssignmentGate();
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Approve_DisabledWhenPartStateIsUnavailable()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "engineer1",
                    partState: null);

                gate.OpenGate(CadBusinessActionKind.Approve);
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Approve_DisabledWhenPartReleaseEvidenceGateIsClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "engineer1",
                    partState: "In Review");

                gate.OpenGate(CadBusinessActionKind.Approve);

                Assert.False(gate.IsPartReleaseAvailable());
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Approve_DisabledWhenAuthorityDoesNotExposeAction()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    System.Array.Empty<CadBusinessAction>(),
                    assigneeName: "engineer1",
                    partState: "In Review");

                gate.OpenGate(CadBusinessActionKind.Approve);
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void SubmitForReview_DisabledWhenAuthorityDoesNotExposeAction()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.DetailedDesign,
                System.Array.Empty<CadBusinessAction>());

            Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.SubmitForReview));
        }

        // F5: RequestRework is blocked when no ActiveTask exists (AssigneeName null).
        [Fact]
        public void RequestRework_DisabledWhenNoAssignee()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
            MainViewModel.SharedUserName = "engineer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                    assigneeName: null);

                gate.OpenGate(CadBusinessActionKind.RequestRework);
                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        // T037: SubmitForReview stays available while Approve/RequestRework/Withdraw
        // are gated.
        [Fact]
        public void SubmitForReview_AvailableWhileReviewActionsGated()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModelWithGate(gate, dialog);
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.DetailedDesign, new List<CadBusinessAction>
            {
                Action(CadBusinessActionKind.SubmitForReview),
                Action(CadBusinessActionKind.Approve),
                Action(CadBusinessActionKind.RequestRework)
            });

            Assert.False(CadWorkflowGate.IsGated(CadBusinessActionKind.SubmitForReview));
            Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.SubmitForReview));
            gate.OpenReviewerAssignmentGate();
            Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.SubmitForReview));
            Assert.True(vm.IsWorkflowGatePending(CadBusinessActionKind.Approve));
            Assert.True(vm.IsWorkflowGatePending(CadBusinessActionKind.RequestRework));
        }

        private sealed class FakeCadClientForReview : IArasCadClient
        {
            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct) => Task.FromResult<ArasLoginResult>(null);
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct) => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct) => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct) => Task.FromResult<FileUploadResult>(null);
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct) => Task.FromResult<CadCheckinResult>(null);
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct) => Task.FromResult<string>(null);
            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct) => Task.FromResult(new CancelCheckoutResult { Success = true });
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct = default)
            {
                var task = new CadWorkflowTask("assignment1", "activity1", "Activity", "wp1", "Active", "engineer1", null);
                var context = new CadOperationContext(
                    cadId, "CAD-001", "A", 1, CadLifecyclePolicy.DetailedDesign, "2026-01-01", true, false, null, null,
                    task, new List<CadBusinessAction> { new CadBusinessAction(CadBusinessActionKind.SubmitForReview, "Submit", true, null, false, "task1", "path1") });
                return Task.FromResult(context);
            }
            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct = default) => Task.FromResult<CadOperationContext>(null);
        }
    }
}
