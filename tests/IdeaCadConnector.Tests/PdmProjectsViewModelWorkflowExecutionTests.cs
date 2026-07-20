using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Desktop.Workflow;
using IdeaCadConnector.Workspace;
using Newtonsoft.Json;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmProjectsViewModelWorkflowExecutionTests
    {
        private sealed class RecordingDialogService : IWorkflowActionDialogService
        {
            public CheckinReasonDialogResult CheckinReasonResult { get; set; }
            public SubmitForReviewDialogResult SubmitResult { get; set; }
            public ReviewDecisionDialogResult ReviewResult { get; set; }
            public bool WithdrawConfirmResult { get; set; } = false;
            public bool GatePendingShown { get; private set; }
            public string LastGateMessage { get; private set; }

            public CheckinReasonDialogResult ShowCheckinReason()
                => CheckinReasonResult ?? new CheckinReasonDialogResult();
            public SubmitForReviewDialogResult ShowSubmitForReview(string cadInfo, string partInfo)
                => SubmitResult ?? new SubmitForReviewDialogResult();
            public ReviewDecisionDialogResult ShowReviewDecision(string submissionInfo, string gateNote)
                => ReviewResult ?? new ReviewDecisionDialogResult();
            public bool ShowWithdrawConfirm(string submissionInfo) => WithdrawConfirmResult;
            public bool ShowGatePending(string title, string message) { GatePendingShown = true; LastGateMessage = message; return false; }
            public bool ShowReviewerUnavailable(string title, string message) => false;
            public bool ConfirmSimple(string title, string message) => false;
        }

        private sealed class StubReleaseEligibility : ICadReleaseEligibility
        {
            public bool IsEligible { get; set; } = true;
            public IReadOnlyList<string> BlockingReasons { get; set; }
            public CadReleaseEligibilitySnapshot LastSnapshot { get; private set; }
            public int CheckAsyncCallCount { get; private set; }

            public Task<CadReleaseEligibilityResult> CheckAsync(
                CadReleaseEligibilitySnapshot snapshot, CancellationToken ct)
            {
                CheckAsyncCallCount++;
                LastSnapshot = snapshot;
                return Task.FromResult(new CadReleaseEligibilityResult
                {
                    IsEligible = IsEligible,
                    BlockingReasons = BlockingReasons ?? (IsEligible ? null : new List<string> { "Mock ineligible" })
                });
            }
        }

        private sealed class StubRevisionService : IRevisionService
        {
            public int ReviseCallCount { get; private set; }

            public Task<PdmRevisePreconditionResult> CheckPreconditionsAsync(
                string cadState,
                string cadId,
                string partId,
                string lockToken,
                CancellationToken ct)
            {
                return Task.FromResult(new PdmRevisePreconditionResult
                {
                    CanRevise = true,
                    BlockingReasons = new List<string>(),
                    Warnings = new List<string>()
                });
            }

            public Task<PdmReviseResult> ReviseAsync(PdmReviseRequest request, CancellationToken ct)
            {
                ReviseCallCount++;
                return Task.FromResult(new PdmReviseResult
                {
                    Success = true,
                    NewPartId = "PART2",
                    NewCadId = "CAD2",
                    NewRevision = "B",
                    NewLifecycleState = PartLifecyclePolicy.KhoiTao
                });
            }
        }

        private sealed class ConcurrentRevisionService : IRevisionService
        {
            private int _requestCount;
            private int _createdPairCount;
            public int CreatedPairCount => _createdPairCount;

            public Task<PdmRevisePreconditionResult> CheckPreconditionsAsync(
                string cadState,
                string cadId,
                string partId,
                string lockToken,
                CancellationToken ct)
            {
                return Task.FromResult(new PdmRevisePreconditionResult
                {
                    CanRevise = true,
                    BlockingReasons = new List<string>(),
                    Warnings = new List<string>()
                });
            }

            public Task<PdmReviseResult> ReviseAsync(PdmReviseRequest request, CancellationToken ct)
            {
                var requestNumber = Interlocked.Increment(ref _requestCount);
                if (requestNumber == 1)
                {
                    Interlocked.Increment(ref _createdPairCount);
                    return Task.FromResult(new PdmReviseResult
                    {
                        Success = true,
                        NewPartId = "PART2",
                        NewCadId = "CAD2",
                        NewRevision = "B",
                        NewLifecycleState = PartLifecyclePolicy.KhoiTao
                    });
                }

                return Task.FromResult(new PdmReviseResult
                {
                    Success = false,
                    ErrorMessage = "Revision conflict: the released pair was already revised."
                });
            }
        }

        private sealed class StubArasCadClient : IArasCadClient
        {
            public CadBusinessActionKind? LastActionKind { get; private set; }
            public string LastComment { get; private set; }
            public bool ExecuteCalled { get; private set; }
            public string AssignedReviewer { get; set; } = "reviewer1";
            public bool CheckinCalled { get; private set; }
            public bool UploadCalled { get; private set; }
            public string LastCheckinComment { get; private set; }

            private CadOperationContext MakeContext() => new CadOperationContext(
                "CAD1", "CAD-001", "A", 1, "In Review", "2026-07-20",
                true, false, null, null,
                new CadWorkflowTask("assignment1", "activity1", "Activity", "wp1", "Active", AssignedReviewer, null),
                new List<CadBusinessAction>
                {
                    new CadBusinessAction(CadBusinessActionKind.Approve, "Approve", true, null, false, "task1", "path1"),
                    new CadBusinessAction(CadBusinessActionKind.RequestRework, "RequestRework", true, null, false, "task1", "path1")
                });

            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(
                ExecuteCadBusinessActionRequest request, CancellationToken ct)
            {
                ExecuteCalled = true;
                LastActionKind = request.Action;
                LastComment = request.Comment;
                return Task.FromResult(MakeContext());
            }

            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct) => Task.FromResult<ArasLoginResult>(null);
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct) => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct) => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
            {
                UploadCalled = true;
                return Task.FromResult(new FileUploadResult { UploadedFileId = "FID1" });
            }
            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct) => Task.FromResult(new CancelCheckoutResult { Success = true });
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
            {
                CheckinCalled = true;
                LastCheckinComment = request.Comment;
                return Task.FromResult(new CadCheckinResult { Success = true });
            }
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct) => Task.FromResult<string>(null);
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct = default) => Task.FromResult(MakeContext());
        }

        private static PdmProjectsViewModel BuildViewModel(
            CadWorkflowGate gate,
            RecordingDialogService dialog,
            ICadReleaseEligibility eligibility,
            IArasCadClient cadClient)
        {
            MainViewModel.SharedArasCadClient = cadClient;
            return new PdmProjectsViewModel(
                new GuidanceRevisionService(),
                new CadLifecyclePolicy(),
                gate,
                dialog,
                eligibility);
        }

        private static void SetLiveContext(
            PdmProjectsViewModel vm,
            string cadId,
            string cadState,
            IReadOnlyList<CadBusinessAction> actions,
            string assigneeName = null,
            string partState = "In Review")
        {
            var task = assigneeName == null
                ? null
                : new CadWorkflowTask("assignment1", "activity1", "Activity", "wp1", "Active", assigneeName, null);
            var context = new CadOperationContext(
                cadId, "CAD-001", "A", 1, cadState, "2026-07-20", true, false, null, null,
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
            typeof(PdmProjectsViewModel)
                .GetField("_livePartId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, "PART1");
        }

        private static CadBusinessAction Action(CadBusinessActionKind kind) =>
            new CadBusinessAction(kind, kind.ToString(), true, null, false, "task1", "path1");

        [Fact]
        public void StartNewRevision_RequiresReleasedPartAndCadPair()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility();
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);
            vm.SelectedNode = new PdmStructureNode(
                "Part 1", "P-001", "Part", 1, "A", "Released", "", primaryCad: "CAD-001");
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.Released,
                new List<CadBusinessAction>(), partState: PartLifecyclePolicy.InReview);
            typeof(PdmProjectsViewModel)
                .GetField("_revisionPreconditions", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PdmRevisePreconditionResult
                {
                    CanRevise = true,
                    BlockingReasons = Array.Empty<string>(),
                    Warnings = Array.Empty<string>()
                });

            Assert.False(vm.CanStartNewRevision);

            typeof(PdmProjectsViewModel)
                .GetField("_livePartState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, PartLifecyclePolicy.Released);

            gate.OpenStartNewRevisionGate();

            Assert.True(vm.CanStartNewRevision);
            MainViewModel.SharedArasCadClient = null;
        }

        [Fact]
        public async Task StartNewRevision_ReleasedPair_UsesRevisionServiceOnce()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility();
            var client = new StubArasCadClient();
            var revisionService = new StubRevisionService();
            MainViewModel.SharedArasCadClient = client;
            var vm = new PdmProjectsViewModel(
                revisionService,
                new CadLifecyclePolicy(),
                gate,
                dialog,
                eligibility);
            vm.SelectedNode = new PdmStructureNode(
                "Part 1", "P-001", "Part", 1, "A", "Released", "", primaryCad: "CAD-001");
            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.Released,
                new List<CadBusinessAction>(), partState: PartLifecyclePolicy.Released);
            gate.OpenStartNewRevisionGate();

            var result = await vm.ExecuteStartNewRevisionCoreAsync();

            Assert.True(result.Success);
            Assert.Equal(1, revisionService.ReviseCallCount);
            Assert.Equal("B", result.NewRevision);
            MainViewModel.SharedArasCadClient = null;
        }

        [Fact]
        public async Task ConcurrentNewRevisionResults_SurfaceConflictWithoutDuplicatePair()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility();
            var client = new StubArasCadClient();
            var revisionService = new ConcurrentRevisionService();
            MainViewModel.SharedArasCadClient = client;

            var vm1 = new PdmProjectsViewModel(revisionService, new CadLifecyclePolicy(), gate, dialog, eligibility);
            var vm2 = new PdmProjectsViewModel(revisionService, new CadLifecyclePolicy(), gate, dialog, eligibility);
            var node1 = new PdmStructureNode("Part 1", "P-001", "Part", 1, "A", "Released", "", primaryCad: "CAD-001");
            var node2 = new PdmStructureNode("Part 1", "P-001", "Part", 1, "A", "Released", "", primaryCad: "CAD-001");
            vm1.SelectedNode = node1;
            vm2.SelectedNode = node2;
            SetLiveContext(vm1, "CAD1", CadLifecyclePolicy.Released, new List<CadBusinessAction>(), partState: PartLifecyclePolicy.Released);
            SetLiveContext(vm2, "CAD1", CadLifecyclePolicy.Released, new List<CadBusinessAction>(), partState: PartLifecyclePolicy.Released);
            SetRevisionPreconditions(vm1);
            SetRevisionPreconditions(vm2);
            gate.OpenStartNewRevisionGate();

            var results = await Task.WhenAll(
                vm1.ExecuteStartNewRevisionCoreAsync(),
                vm2.ExecuteStartNewRevisionCoreAsync());

            Assert.Equal(1, results.Count(result => result.Success));
            Assert.Equal(1, results.Count(result => !result.Success));
            Assert.Contains(results, result =>
                !result.Success && result.ErrorMessage.Contains("conflict"));
            Assert.Equal(1, revisionService.CreatedPairCount);
            MainViewModel.SharedArasCadClient = null;
        }

        private static void SetRevisionPreconditions(PdmProjectsViewModel vm)
        {
            typeof(PdmProjectsViewModel)
                .GetField("_revisionPreconditions", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PdmRevisePreconditionResult
                {
                    CanRevise = true,
                    BlockingReasons = Array.Empty<string>(),
                    Warnings = Array.Empty<string>()
                });
        }

        [Fact]
        public async Task Approve_Eligible_CallsAuthority()
        {
            var gate = new CadWorkflowGate();
            gate.OpenGate(CadBusinessActionKind.Approve);
            gate.OpenReviewerAssignmentGate();
            gate.OpenPartReleaseGate();
            var dialog = new RecordingDialogService
            {
                ReviewResult = new ReviewDecisionDialogResult { Confirmed = true, Kind = CadBusinessActionKind.Approve, Comment = "Approved" }
            };
            var eligibility = new StubReleaseEligibility { IsEligible = true };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);

            MainViewModel.SharedUserName = "reviewer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "reviewer1");

                Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));

                vm.ApproveCadCommand.Execute(null);

                Assert.True(client.ExecuteCalled);
                Assert.Equal(CadBusinessActionKind.Approve, client.LastActionKind);
                Assert.Equal(1, eligibility.CheckAsyncCallCount);
                Assert.NotNull(eligibility.LastSnapshot);
                Assert.Equal("CAD1", eligibility.LastSnapshot.CadId);
                Assert.Equal("PART1", eligibility.LastSnapshot.PartId);
                Assert.Equal(CadLifecyclePolicy.InReview, eligibility.LastSnapshot.CadState);
                Assert.Equal("In Review", eligibility.LastSnapshot.PartState);
            }
            finally
            {
                MainViewModel.SharedUserName = null;
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public async Task Approve_Ineligible_DoesNotCallAuthority()
        {
            var gate = new CadWorkflowGate();
            gate.OpenGate(CadBusinessActionKind.Approve);
            gate.OpenReviewerAssignmentGate();
            gate.OpenPartReleaseGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility { IsEligible = false, BlockingReasons = new List<string> { "Part is not eligible for release" } };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);

            MainViewModel.SharedUserName = "reviewer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.Approve) },
                    assigneeName: "reviewer1");

                Assert.True(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.Approve));

                vm.ApproveCadCommand.Execute(null);

                Assert.False(client.ExecuteCalled);
                Assert.Equal(1, eligibility.CheckAsyncCallCount);
                Assert.NotNull(eligibility.LastSnapshot);
                Assert.Equal("CAD1", eligibility.LastSnapshot.CadId);
                Assert.Equal("PART1", eligibility.LastSnapshot.PartId);
                Assert.Equal("In Review", eligibility.LastSnapshot.PartState);
            }
            finally
            {
                MainViewModel.SharedUserName = null;
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public async Task RequestRework_PassesCommentToAuthority()
        {
            var gate = new CadWorkflowGate();
            gate.OpenGate(CadBusinessActionKind.RequestRework);
            gate.OpenReviewerAssignmentGate();
            var dialog = new RecordingDialogService
            {
                ReviewResult = new ReviewDecisionDialogResult { Confirmed = true, Kind = CadBusinessActionKind.RequestRework, Comment = "Fix the mounting holes" }
            };
            var eligibility = new StubReleaseEligibility { IsEligible = true };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);

            MainViewModel.SharedUserName = "reviewer1";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                    assigneeName: "reviewer1");

                vm.RequestReworkCommand.Execute(null);

                Assert.True(client.ExecuteCalled);
                Assert.Equal(CadBusinessActionKind.RequestRework, client.LastActionKind);
                Assert.Equal("Fix the mounting holes", client.LastComment);
            }
            finally
            {
                MainViewModel.SharedUserName = null;
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public void Rework_BlockedWhenGateClosed()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility();
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);

            SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                assigneeName: "reviewer1");

            Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));

            vm.RequestReworkCommand.Execute(null);

            Assert.False(client.ExecuteCalled);
        }

        [Fact]
        public void Rework_BlockedWhenReviewerMismatch()
        {
            var gate = new CadWorkflowGate();
            gate.OpenGate(CadBusinessActionKind.RequestRework);
            gate.OpenReviewerAssignmentGate();
            var dialog = new RecordingDialogService();
            var eligibility = new StubReleaseEligibility();
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, eligibility, client);

            MainViewModel.SharedUserName = "wrongReviewer";
            try
            {
                SetLiveContext(vm, "CAD1", CadLifecyclePolicy.InReview,
                    new List<CadBusinessAction> { Action(CadBusinessActionKind.RequestRework) },
                    assigneeName: "correctReviewer");

                Assert.False(vm.CanExecuteCadBusinessAction(CadBusinessActionKind.RequestRework));

                vm.RequestReworkCommand.Execute(null);

                Assert.False(client.ExecuteCalled);
            }
            finally
            {
                MainViewModel.SharedUserName = null;
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public void CheckInReason_Cancel_DoesNotCallAuthority()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = false }
            };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, new StubReleaseEligibility(), client);

            var folder = Path.Combine(Path.GetTempPath(), "pdm-checkin-cancel-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var manifest = new WorkspaceManifest
                {
                    CadId = "CAD1",
                    LockToken = "TOKEN1",
                    ProjectFolder = folder,
                    LocalFilePath = Path.Combine(folder, "test.ics")
                };
                File.WriteAllText(manifest.LocalFilePath, "dummy content");
                var wsService = new WorkspaceService(new WorkspaceOptions());
                wsService.SaveManifest(manifest);

                vm.FolderPath = folder;
                vm._checkoutService = new CheckoutService(client, wsService);

                vm.CheckInCommand.Execute(null);

                var checkinCalled = client.CheckinCalled;
                Assert.False(checkinCalled);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public void CheckInReason_ValidReason_PassesCorrectComment()
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult
                {
                    Confirmed = true,
                    Reason = "Fixed mounting holes"
                }
            };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, new StubReleaseEligibility(), client);

            var folder = Path.Combine(Path.GetTempPath(), "pdm-checkin-reason-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var manifest = new WorkspaceManifest
                {
                    CadId = "CAD1",
                    LockToken = "TOKEN1",
                    ProjectFolder = folder,
                    LocalFilePath = Path.Combine(folder, "test.ics")
                };
                File.WriteAllText(manifest.LocalFilePath, "dummy content");
                var wsService = new WorkspaceService(new WorkspaceOptions());
                wsService.SaveManifest(manifest);

                vm.FolderPath = folder;
                vm._checkoutService = new CheckoutService(client, wsService);

                vm.CheckInCommand.Execute(null);

                Assert.True(client.CheckinCalled);
                Assert.Equal("Fixed mounting holes", client.LastCheckinComment);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CheckInReason_ConfirmedWithEmptyReason_DoesNotCallAuthority(string reason)
        {
            var gate = new CadWorkflowGate();
            var dialog = new RecordingDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult
                {
                    Confirmed = true,
                    Reason = reason
                }
            };
            var client = new StubArasCadClient();
            var vm = BuildViewModel(gate, dialog, new StubReleaseEligibility(), client);

            var folder = Path.Combine(Path.GetTempPath(), "pdm-checkin-empty-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var manifest = new WorkspaceManifest
                {
                    CadId = "CAD1",
                    LockToken = "TOKEN1",
                    ProjectFolder = folder,
                    LocalFilePath = Path.Combine(folder, "test.ics")
                };
                File.WriteAllText(manifest.LocalFilePath, "dummy content");
                var wsService = new WorkspaceService(new WorkspaceOptions());
                wsService.SaveManifest(manifest);

                vm.FolderPath = folder;
                vm._checkoutService = new CheckoutService(client, wsService);

                vm.CheckInCommand.Execute(null);

                Assert.False(client.UploadCalled);
                Assert.False(client.CheckinCalled);
                Assert.True(wsService.LoadManifest(folder) != null);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
                MainViewModel.SharedArasCadClient = null;
            }
        }

        [Fact]
        public void CheckInReason_BothViewModelsRejectEmptyReasonBeforeAuthority()
        {
            string[] emptyReasons = { null, "", "   " };
            foreach (var reason in emptyReasons)
            {
                var gate = new CadWorkflowGate();
                var dialog = new RecordingDialogService
                {
                    CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = true, Reason = reason }
                };
                var client = new StubArasCadClient();
                var vm = BuildViewModel(gate, dialog, new StubReleaseEligibility(), client);

                var folder = Path.Combine(Path.GetTempPath(), "pdm-parity-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(folder);
                    var manifest = new WorkspaceManifest
                    {
                        CadId = "CAD1",
                        LockToken = "TOKEN1",
                        ProjectFolder = folder,
                        LocalFilePath = Path.Combine(folder, "test.ics")
                    };
                    File.WriteAllText(manifest.LocalFilePath, "dummy content");
                    var wsService = new WorkspaceService(new WorkspaceOptions());
                    wsService.SaveManifest(manifest);

                    vm.FolderPath = folder;
                    vm._checkoutService = new CheckoutService(client, wsService);

                    vm.CheckInCommand.Execute(null);

                    Assert.False(client.UploadCalled);
                    Assert.False(client.CheckinCalled);
                }
                finally
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, recursive: true);
                    MainViewModel.SharedArasCadClient = null;
                }
            }
        }
    }
}
