using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Workflow;
using IdeaCadConnector.Workspace;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class MainViewModelWorkflowGatingTests
    {
        private sealed class NoOpDialogService : IWorkflowActionDialogService
        {
            public CheckinReasonDialogResult CheckinReasonResult { get; set; } = new CheckinReasonDialogResult();
            public CheckinReasonDialogResult ShowCheckinReason()
                => CheckinReasonResult;
            public SubmitForReviewDialogResult ShowSubmitForReview(string cadInfo, string partInfo)
                => new SubmitForReviewDialogResult();
            public ReviewDecisionDialogResult ShowReviewDecision(string submissionInfo, string gateNote)
                => new ReviewDecisionDialogResult();
            public bool ShowWithdrawConfirm(string submissionInfo) => false;
            public bool ShowGatePending(string title, string message) => false;
            public bool ShowReviewerUnavailable(string title, string message) => false;
            public bool ConfirmSimple(string title, string message) => false;
        }

        private static MainViewModel BuildViewModel(
            CadBusinessActionKind actionKind,
            string cadState,
            string assigneeName = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "idea-pdm-main-vm-tests", Guid.NewGuid().ToString("N"));
            var vm = new MainViewModel(
                new ArasClientOptions(),
                null,
                new WorkspaceService(new WorkspaceOptions { RootPath = root }),
                new NoOpDialogService());

            SetPrivateField(vm, "_selectedCadId", "CAD1");
            vm._currentPartState = "In Review";
            SetPrivateField(vm, "_currentCad", new CadSummary
            {
                Id = "CAD1",
                CadNumber = "CAD-001",
                State = cadState,
                HasNativeFile = true
            });

            var task = assigneeName == null
                ? null
                : new CadWorkflowTask("assignment1", "activity1", "Review", "wp1", "Active", assigneeName, null);
            var context = new CadOperationContext(
                "CAD1",
                "CAD-001",
                "A",
                1,
                cadState,
                "2026-01-01",
                true,
                false,
                null,
                null,
                task,
                new List<CadBusinessAction>
                {
                    new CadBusinessAction(actionKind, actionKind.ToString(), true, null, false, "task1", "path1")
                });
            SetPrivateField(vm, "_cadOperationContext", context);
            return vm;
        }

        private static void SetPrivateField<T>(MainViewModel vm, string fieldName, T value)
        {
            typeof(MainViewModel)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(vm, value);
        }

        [Fact]
        public void SubmitForReview_CommandIsBlockedUntilReviewerAssignmentEvidenceOpens()
        {
            var vm = BuildViewModel(CadBusinessActionKind.SubmitForReview, CadLifecyclePolicy.DetailedDesign);

            Assert.False(vm.SubmitForReviewCommand.CanExecute(null));

            vm._workflowGate.OpenReviewerAssignmentGate();

            Assert.True(vm.SubmitForReviewCommand.CanExecute(null));
        }

        [Fact]
        public void Approve_CommandRequiresPartReleaseEvidence()
        {
            MainViewModel.SharedUserName = "reviewer1";
            try
            {
                var vm = BuildViewModel(CadBusinessActionKind.Approve, CadLifecyclePolicy.InReview, "reviewer1");
                vm._workflowGate.OpenGate(CadBusinessActionKind.Approve);

                Assert.False(vm.ApproveCommand.CanExecute(null));

                vm._workflowGate.OpenPartReleaseGate();
                // Part-release gate alone is insufficient; reviewer assignment
                // gate must also be open for Approve/RequestRework.
                Assert.False(vm.ApproveCommand.CanExecute(null));

                vm._workflowGate.OpenReviewerAssignmentGate();

                Assert.True(vm.ApproveCommand.CanExecute(null));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Approve_CommandRequiresCurrentAssignedReviewer()
        {
            MainViewModel.SharedUserName = "reviewer1";
            try
            {
                var vm = BuildViewModel(CadBusinessActionKind.Approve, CadLifecyclePolicy.InReview, "reviewer2");
                vm._workflowGate.OpenGate(CadBusinessActionKind.Approve);
                vm._workflowGate.OpenPartReleaseGate();
                vm._workflowGate.OpenReviewerAssignmentGate();

                Assert.False(vm.ApproveCommand.CanExecute(null));
            }
            finally
            {
                MainViewModel.SharedUserName = null;
            }
        }

        [Fact]
        public void Withdraw_CommandRemainsFailClosedWithoutAuthoritativeOwner()
        {
            var vm = BuildViewModel(CadBusinessActionKind.Withdraw, CadLifecyclePolicy.InReview, "engineer1");
            vm._workflowGate.OpenGate(CadBusinessActionKind.Withdraw);

            Assert.False(vm.WithdrawCommand.CanExecute(null));
        }

        [Fact]
        public void ReleasedPart_BlocksCheckoutEvenWhenCadIsInDesignState()
        {
            var vm = BuildViewModel(
                CadBusinessActionKind.StartDetailedDesign,
                CadLifecyclePolicy.DetailedDesign);
            vm._currentPartState = PartLifecyclePolicy.Released;

            Assert.False(vm.CheckoutCommand.CanExecute(null));
        }

        [Fact]
        public void StartNewRevision_RequiresReleasedPartPair()
        {
            var vm = BuildViewModel(
                CadBusinessActionKind.StartDetailedDesign,
                CadLifecyclePolicy.Released);
            vm._currentPartState = PartLifecyclePolicy.Released;
            SetPrivateField(vm, "_revisionPreconditions", new PdmRevisePreconditionResult
            {
                CanRevise = true,
                BlockingReasons = Array.Empty<string>(),
                Warnings = Array.Empty<string>()
            });

            vm._workflowGate.OpenStartNewRevisionGate();

            Assert.True(vm.CanStartNewRevision);

            vm._currentPartState = PartLifecyclePolicy.InReview;

            Assert.False(vm.CanStartNewRevision);
        }

        [Fact]
        public void StartNewRevision_RequiresReleasedCadPair()
        {
            var vm = BuildViewModel(
                CadBusinessActionKind.StartDetailedDesign,
                CadLifecyclePolicy.InReview);
            vm._currentPartState = PartLifecyclePolicy.Released;
            SetPrivateField(vm, "_revisionPreconditions", new PdmRevisePreconditionResult
            {
                CanRevise = true,
                BlockingReasons = Array.Empty<string>(),
                Warnings = Array.Empty<string>()
            });
            vm._workflowGate.OpenStartNewRevisionGate();

            Assert.False(vm.CanStartNewRevision);
        }

        [Fact]
        public void CheckInReason_Cancel_DoesNotCallAuthority()
        {
            var client = new StubArasCadClient();
            var dialog = new NoOpDialogService { CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = false } };
            var root = Path.Combine(Path.GetTempPath(), "idea-pdm-checkin-vm", Guid.NewGuid().ToString("N"));
            var wsService = new WorkspaceService(new WorkspaceOptions { RootPath = root });
            var vm = new MainViewModel(new ArasClientOptions(), null, wsService, dialog);

            SetPrivateField(vm, "_arasClient", client);
            SetPrivateField(vm, "_loginResult", new ArasLoginResult { SessionToken = "s1" });
            SetPrivateField(vm, "_selectedCadId", "CAD1");
            vm._lockToken = "TOKEN1";

            vm.CheckInCommand.Execute(null);

            Assert.False(client.CheckinCalled);
        }

        [Fact]
        public void CheckInReason_ValidReason_PassesCorrectComment()
        {
            var client = new StubArasCadClient();
            var dialog = new NoOpDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult
                {
                    Confirmed = true,
                    Reason = "Updated Bill of Materials"
                }
            };
            var root = Path.Combine(Path.GetTempPath(), "idea-pdm-checkin-vm2", Guid.NewGuid().ToString("N"));
            var wsService = new WorkspaceService(new WorkspaceOptions { RootPath = root });
            var vm = new MainViewModel(new ArasClientOptions(), null, wsService, dialog);

            var filePath = Path.Combine(root, "test.ics");
            Directory.CreateDirectory(root);
            File.WriteAllText(filePath, "dummy");

            SetPrivateField(vm, "_arasClient", client);
            SetPrivateField(vm, "_loginResult", new ArasLoginResult { SessionToken = "s1" });
            SetPrivateField(vm, "_selectedCadId", "CAD1");
            vm._lockToken = "TOKEN1";
            vm._lastDownloadedFilePath = filePath;
            vm._checkoutService = new CheckoutService(client, wsService);

            vm.CheckInCommand.Execute(null);

            Assert.True(client.CheckinCalled);
            Assert.Equal("Updated Bill of Materials", client.LastCheckinComment);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CheckInReason_ConfirmedWithEmptyReason_DoesNotCallAuthority(string reason)
        {
            var client = new StubArasCadClient();
            var dialog = new NoOpDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = true, Reason = reason }
            };
            var root = Path.Combine(Path.GetTempPath(), "idea-pdm-checkin-empty-" + Guid.NewGuid().ToString("N"));
            var wsService = new WorkspaceService(new WorkspaceOptions { RootPath = root });
            var vm = new MainViewModel(new ArasClientOptions(), null, wsService, dialog);

            var filePath = Path.Combine(root, "test.ics");
            Directory.CreateDirectory(root);
            File.WriteAllText(filePath, "dummy");

            SetPrivateField(vm, "_arasClient", client);
            SetPrivateField(vm, "_loginResult", new ArasLoginResult { SessionToken = "s1" });
            SetPrivateField(vm, "_selectedCadId", "CAD1");
            vm._lockToken = "TOKEN1";
            vm._lastDownloadedFilePath = filePath;
            vm._checkoutService = new CheckoutService(client, wsService);

            vm.CheckInCommand.Execute(null);

            Assert.False(client.UploadCalled);
            Assert.False(client.CheckinCalled);
        }

        private static (StubArasCadClient client, string lockTokenAfter) RunMainViewModelCheckIn(
            string reason)
        {
            var client = new StubArasCadClient();
            var dialog = new NoOpDialogService
            {
                CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = true, Reason = reason }
            };
            var root = Path.Combine(Path.GetTempPath(), "idea-pdm-parity-" + Guid.NewGuid().ToString("N"));
            var wsService = new WorkspaceService(new WorkspaceOptions { RootPath = root });
            var vm = new MainViewModel(new ArasClientOptions(), null, wsService, dialog);

            var filePath = Path.Combine(root, "test.ics");
            Directory.CreateDirectory(root);
            File.WriteAllText(filePath, "dummy");

            SetPrivateField(vm, "_arasClient", client);
            SetPrivateField(vm, "_loginResult", new ArasLoginResult { SessionToken = "s1" });
            SetPrivateField(vm, "_selectedCadId", "CAD1");
            vm._lockToken = "TOKEN1";
            vm._lastDownloadedFilePath = filePath;
            vm._checkoutService = new CheckoutService(client, wsService);

            vm.CheckInCommand.Execute(null);
            return (client, vm._lockToken);
        }

        private sealed class StubArasCadClient : IArasCadClient
        {
            public bool CheckinCalled { get; private set; }
            public bool UploadCalled { get; private set; }
            public string LastCheckinComment { get; private set; }

            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
                => Task.FromResult(new ArasLoginResult { SessionToken = "s1" });
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
                => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
                => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
                => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
                => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
            {
                UploadCalled = true;
                return Task.FromResult(new FileUploadResult { UploadedFileId = "FID1" });
            }
            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CancelCheckoutResult { Success = true });
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
            {
                CheckinCalled = true;
                LastCheckinComment = request.Comment;
                return Task.FromResult(new CadCheckinResult { Success = true });
            }
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
                => Task.FromResult<string>(null);
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct = default)
                => Task.FromResult<CadOperationContext>(null);
            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct)
                => Task.FromResult<CadOperationContext>(null);
        }

        [Fact]
        public void CheckInReason_BothViewModelsUseSameRejectionPathForEmptyReason()
        {
            string[] emptyReasons = { null, "", "   " };
            foreach (var reason in emptyReasons)
            {
                var (mainClient, mainLock) = RunMainViewModelCheckIn(reason);

                var gate = new CadWorkflowGate();
                var dialog = new NoOpDialogService
                {
                    CheckinReasonResult = new CheckinReasonDialogResult { Confirmed = true, Reason = reason }
                };
                var pdmClient = new PdmProjectsStubArasCadClient();
                var pdmVm = new PdmProjectsViewModel(
                    new GuidanceRevisionService(),
                    new CadLifecyclePolicy(),
                    gate,
                    dialog,
                    new StubReleaseEligibilityForParity());

                var folder = Path.Combine(Path.GetTempPath(), "idea-pdm-parity2-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                var manifest = new WorkspaceManifest
                {
                    CadId = "CAD1",
                    LockToken = "TOKEN1",
                    ProjectFolder = folder,
                    LocalFilePath = Path.Combine(folder, "test.ics")
                };
                File.WriteAllText(manifest.LocalFilePath, "dummy");
                var wsService = new WorkspaceService(new WorkspaceOptions());
                wsService.SaveManifest(manifest);
                pdmVm.FolderPath = folder;
                pdmVm._checkoutService = new CheckoutService(pdmClient, wsService);

                pdmVm.CheckInCommand.Execute(null);

                Assert.False(mainClient.UploadCalled);
                Assert.False(mainClient.CheckinCalled);
                Assert.Equal("TOKEN1", mainLock);
                Assert.False(pdmClient.UploadCalled);
                Assert.False(pdmClient.CheckinCalled);
                Assert.True(wsService.LoadManifest(folder) != null);
            }
        }

        private sealed class PdmProjectsStubArasCadClient : IArasCadClient
        {
            public bool CheckinCalled { get; private set; }
            public bool UploadCalled { get; private set; }
            public string LastCheckinComment { get; private set; }

            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
                => Task.FromResult(new ArasLoginResult { SessionToken = "s1" });
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
                => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
                => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
                => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
                => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
            {
                UploadCalled = true;
                return Task.FromResult(new FileUploadResult { UploadedFileId = "FID1" });
            }
            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CancelCheckoutResult { Success = true });
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
            {
                CheckinCalled = true;
                LastCheckinComment = request.Comment;
                return Task.FromResult(new CadCheckinResult { Success = true });
            }
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
                => Task.FromResult<string>(null);
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct = default)
                => Task.FromResult<CadOperationContext>(null);
            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct)
                => Task.FromResult<CadOperationContext>(null);
        }

        private sealed class StubReleaseEligibilityForParity : ICadReleaseEligibility
        {
            public Task<CadReleaseEligibilityResult> CheckAsync(
                CadReleaseEligibilitySnapshot snapshot, CancellationToken ct)
                => Task.FromResult(new CadReleaseEligibilityResult { IsEligible = true });
        }
    }
}
