using System;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Desktop.Workflow;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmProjectsViewModelCadResolutionTests : IDisposable
    {
        private sealed class SpyCadClient : IArasCadClient
        {
            public string CadIdToReturn { get; set; }
            public string PartIdPassed { get; private set; }

            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct) => Task.FromResult<ArasLoginResult>(null);
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct) => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct) => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct) => Task.FromResult<FileUploadResult>(null);
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct) => Task.FromResult<CadCheckinResult>(null);
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct) => Task.FromResult<string>(null);
            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct) => Task.FromResult<CancelCheckoutResult>(null);
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct) => Task.FromResult<CadOperationContext>(null);
            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct) => Task.FromResult<CadOperationContext>(null);

            public Task<string> GetPrimaryCadIdForPartAsync(string partId, CancellationToken ct)
            {
                PartIdPassed = partId;
                return Task.FromResult(CadIdToReturn);
            }
        }

        private sealed class SpyPdmClient : IPdmRepositoryClient
        {
            public string PartIdToReturn { get; set; }
            public string LastItemType { get; private set; }
            public string LastItemNumber { get; private set; }
            public int CallCount { get; private set; }

            public void Dispose() { }

            public Task<PdmPushResult> PushAsync(PdmPushRequest request, CancellationToken ct) => Task.FromResult<PdmPushResult>(null);
            public Task<PdmExistencePreview> PreviewExistenceAsync(PdmPushRequest request, CancellationToken ct) => Task.FromResult<PdmExistencePreview>(null);
            public Task<PdmCloneResult> CloneLatestToWorkspaceAsync(PdmCloneRequest request, CancellationToken ct) => Task.FromResult<PdmCloneResult>(null);
            public Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct) => Task.FromResult(new PdmReviseResult { Success = false });

            public Task<string> FindItemIdByNumberAsync(string itemType, string itemNumber, CancellationToken ct)
            {
                CallCount++;
                LastItemType = itemType;
                LastItemNumber = itemNumber;
                return Task.FromResult(PartIdToReturn);
            }
        }

        private sealed class StubDialogService : IWorkflowActionDialogService
        {
            public CheckinReasonDialogResult ShowCheckinReason() => new CheckinReasonDialogResult();
            public SubmitForReviewDialogResult ShowSubmitForReview(string cadInfo, string partInfo) => new SubmitForReviewDialogResult();
            public ReviewDecisionDialogResult ShowReviewDecision(string submissionInfo, string gateNote) => new ReviewDecisionDialogResult();
            public bool ShowWithdrawConfirm(string submissionInfo) => true;
            public bool ShowGatePending(string title, string message) => false;
            public bool ShowReviewerUnavailable(string title, string message) => false;
            public bool ShowWorkflowActionError(string title, string message) => false;
            public bool ConfirmSimple(string title, string message) => false;
        }

        private sealed class StubReleaseEligibility : ICadReleaseEligibility
        {
            public Task<CadReleaseEligibilityResult> CheckAsync(CadReleaseEligibilitySnapshot snapshot, CancellationToken ct)
                => Task.FromResult(new CadReleaseEligibilityResult { IsEligible = true });
        }

        public void Dispose()
        {
            MainViewModel.SharedArasCadClient = null;
            (MainViewModel.SharedPdmClient as IDisposable)?.Dispose();
            MainViewModel.SharedPdmClient = null;
        }

        private static PdmProjectsViewModel BuildViewModel(IArasCadClient cadClient)
        {
            MainViewModel.SharedArasCadClient = cadClient;
            return new PdmProjectsViewModel(
                new GuidanceRevisionService(),
                new CadLifecyclePolicy(),
                new CadWorkflowGate(),
                new StubDialogService(),
                new StubReleaseEligibility(),
                new TestPdmRoleProvider());
        }

        // ── Path 1: Node has ArasPartId ──────────────────────────────────────

        [Fact]
        public async Task Resolve_WhenNodeHasArasPartId_ReturnsCadId()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "PART-999");

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal("CAD-999", result);
        }

        [Fact]
        public async Task Resolve_WhenNodeHasArasPartId_PassesThatIdToCadLookup()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "PART-999");

            await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal("PART-999", client.PartIdPassed);
        }

        [Fact]
        public async Task Resolve_WhenCadClientIsNull_ReturnsNull()
        {
            var vm = BuildViewModel(null);
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "PART-999");

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_WhenGetPrimaryCadReturnsNull_ReturnsNull()
        {
            var client = new SpyCadClient { CadIdToReturn = null };
            var vm = BuildViewModel(client);
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "PART-999");

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_WhenNodeIsNull_ReturnsNull()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);

            var result = await vm.ResolveCadIdForNodeAsync(null, CancellationToken.None);

            Assert.Null(result);
        }

        // ── Path 2: Node has no ArasPartId, no manifest, no PDM client ─────

        [Fact]
        public async Task Resolve_WhenNoArasPartIdAndNoPdmClient_ReturnsNull()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            MainViewModel.SharedPdmClient = null;
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: null);

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_WhenArasPartIdEmptyAndNoPdmClient_ReturnsNull()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            MainViewModel.SharedPdmClient = null;
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "");

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
        }

        // ── Path 3: PartCode + PDM client resolves Part ID, then into CAD lookup ─

        [Fact]
        public async Task Resolve_WhenNoArasPartId_ResolvesPartCodeViaPdmClient_ThenResolvesCad()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-RESOLVED" };
            var vm = BuildViewModel(client);
            MainViewModel.SharedPdmClient = new SpyPdmClient { PartIdToReturn = "PART-RESOLVED" };
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: null);

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal("CAD-RESOLVED", result);
        }

        [Fact]
        public async Task Resolve_WhenNoArasPartId_PassesResolvedPartIdToCadLookup()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-RESOLVED" };
            var vm = BuildViewModel(client);
            MainViewModel.SharedPdmClient = new SpyPdmClient { PartIdToReturn = "PART-RESOLVED" };
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: null);

            await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal("PART-RESOLVED", client.PartIdPassed);
        }

        [Fact]
        public async Task Resolve_WhenNoArasPartId_PdmClientCalledWithItemTypePartAndPartCode()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-RESOLVED" };
            var vm = BuildViewModel(client);
            var pdm = new SpyPdmClient { PartIdToReturn = "PART-RESOLVED" };
            MainViewModel.SharedPdmClient = pdm;
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: null);

            await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal(1, pdm.CallCount);
            Assert.Equal("Part", pdm.LastItemType);
            Assert.Equal("P-999", pdm.LastItemNumber);
        }

        [Fact]
        public async Task Resolve_WhenPdmClientReturnsNull_ReturnsNull()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            MainViewModel.SharedPdmClient = new SpyPdmClient { PartIdToReturn = null };
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: null);

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_WhenArasPartIdExists_PdmClientNotCalled()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            var pdm = new SpyPdmClient { PartIdToReturn = "PART-OTHER" };
            MainViewModel.SharedPdmClient = pdm;
            var node = new PdmStructureNode(
                "Part X", "P-999", "Part", 1, "A", "Released", "",
                arasPartId: "PART-999");

            await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Equal(0, pdm.CallCount);
            Assert.Equal("PART-999", client.PartIdPassed);
        }

        [Fact]
        public async Task Resolve_WhenNodeHasNullPartCode_DoesNotCallPdmClient()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            var pdm = new SpyPdmClient { PartIdToReturn = "PART-X" };
            MainViewModel.SharedPdmClient = pdm;
            var node = new PdmStructureNode(
                "Part X", null, "Part", 1, "A", "Released", "",
                arasPartId: null);

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, pdm.CallCount);
        }

        [Fact]
        public async Task Resolve_WhenNodeHasEmptyPartCode_DoesNotCallPdmClient()
        {
            var client = new SpyCadClient { CadIdToReturn = "CAD-999" };
            var vm = BuildViewModel(client);
            var pdm = new SpyPdmClient { PartIdToReturn = "PART-X" };
            MainViewModel.SharedPdmClient = pdm;
            var node = new PdmStructureNode(
                "Part X", "", "Part", 1, "A", "Released", "",
                arasPartId: null);

            var result = await vm.ResolveCadIdForNodeAsync(node, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, pdm.CallCount);
        }
    }
}
