using System.Collections.Generic;
using System.Reflection;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Workflow;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class ReviewerEnforcementTests
    {
        private sealed class FakeReviewerProvider : IReviewerProvider
        {
            public IReadOnlyList<string> Reviewers { get; set; } = System.Array.Empty<string>();
            public IReadOnlyList<string> GetReviewers() => Reviewers;
        }

        private sealed class RecordingDialogService : IWorkflowActionDialogService
        {
            public bool ReviewerUnavailableShown { get; private set; }
            public bool GatePendingShown { get; private set; }

            public SubmitForReviewDialogResult ShowSubmitForReview(string cadInfo, string partInfo, IEnumerable<string> reviewers)
                => new SubmitForReviewDialogResult { Confirmed = false };

            public ReviewDecisionDialogResult ShowReviewDecision(string submissionInfo, string gateNote)
                => new ReviewDecisionDialogResult { Confirmed = false };

            public bool ShowWithdrawConfirm(string submissionInfo) => false;

            public bool ShowGatePending(string title, string message)
            {
                GatePendingShown = true;
                return false;
            }

            public bool ShowReviewerUnavailable(string title, string message)
            {
                ReviewerUnavailableShown = true;
                return false;
            }

            public bool ConfirmSimple(string title, string message) => false;
        }

        private static PdmProjectsViewModel BuildViewModel(
            CadWorkflowGate gate, IReviewerProvider reviewerProvider, RecordingDialogService dialog)
        {
            return new PdmProjectsViewModel(
                new GuidanceRevisionService(),
                new CadLifecyclePolicy(),
                gate,
                dialog,
                PdmProjectsViewModel.CreateDefaultReleaseEligibility(new CadLifecyclePolicy(), gate),
                reviewerProvider);
        }

        private static void SetLiveContext(
            PdmProjectsViewModel vm, string cadId, string cadState, IReadOnlyList<CadBusinessAction> actions, string assigneeName = null)
        {
            var task = assigneeName == null
                ? null
                : new CadWorkflowTask("assignment1", "activity1", "Activity", "wp1", "Active", assigneeName, null);
            var context = new CadOperationContext(
                cadId, "CAD-001", "A", 1, cadState, "2026-01-01", true, false, null, null,
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
        }

        private static CadBusinessAction Action(CadBusinessActionKind kind) =>
            new CadBusinessAction(kind, kind.ToString(), true, null, false, "task1", "path1");

        // T023: backend-neutral provider returns no placeholder identities.
        [Fact]
        public void EmptyReviewerProvider_ReturnsNoReviewers()
        {
            var provider = new EmptyReviewerProvider();

            var reviewers = provider.GetReviewers();

            Assert.NotNull(reviewers);
            Assert.Empty(reviewers);
        }

        // T023: when no authoritative reviewer exists, submit-for-review is blocked
        // with a clear message and no placeholder reviewer is used.
        [Fact]
        public void SubmitForReview_NoReviewer_BlockedWithMessage()
        {
            var dialog = new RecordingDialogService();
            var reviewerProvider = new FakeReviewerProvider { Reviewers = System.Array.Empty<string>() };
            var gate = new CadWorkflowGate();
            var vm = BuildViewModel(gate, reviewerProvider, dialog);
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.DetailedDesign, new List<CadBusinessAction> { Action(CadBusinessActionKind.SubmitForReview) });

            MainViewModel.SharedUserName = "engineer1";
            MainViewModel.SharedArasCadClient = new FakeCadClientForReview();

            try
            {
                vm.ExecuteSubmitForReviewAsync();

                Assert.True(dialog.ReviewerUnavailableShown);
                Assert.Equal(
                    PdmProjectsViewModel.Localize(TranslationKeys.ReviewerUnavailable),
                    vm.StatusMessage);
            }
            finally
            {
                MainViewModel.SharedArasCadClient = null;
                MainViewModel.SharedUserName = null;
            }
        }

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
            var vm = BuildViewModel(new CadWorkflowGate(), new EmptyReviewerProvider(), new RecordingDialogService());
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

        // T033: Withdraw remains disabled while GATE-W is closed and no
        // authoritative owner/submitter field exists.
        [Fact]
        public void Withdraw_IsDisabledWhileGateClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModel(gate, new EmptyReviewerProvider(), dialog);
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview, new List<CadBusinessAction> { Action(CadBusinessActionKind.Withdraw) });

            Assert.True(CadWorkflowGate.IsGated(CadBusinessActionKind.Withdraw));
            Assert.True(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
            Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));

            gate.OpenGate(CadBusinessActionKind.Withdraw);
            Assert.False(vm.IsWorkflowGatePending(CadBusinessActionKind.Withdraw));
            Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Withdraw));
        }

        // T037: SubmitForReview stays available while Approve/RequestRework/Withdraw
        // are gated.
        [Fact]
        public void SubmitForReview_AvailableWhileReviewActionsGated()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var vm = BuildViewModel(gate, new EmptyReviewerProvider(), dialog);
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.DetailedDesign, new List<CadBusinessAction>
            {
                Action(CadBusinessActionKind.SubmitForReview),
                Action(CadBusinessActionKind.Approve),
                Action(CadBusinessActionKind.RequestRework)
            });

            Assert.False(CadWorkflowGate.IsGated(CadBusinessActionKind.SubmitForReview));
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
