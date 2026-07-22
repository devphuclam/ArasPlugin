using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Workspace;
using IdeaCadConnector.Workspace.Models;
using IdeaCadConnector.Workspace.Recovery;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmProjectsCancelCheckoutTests
    {
        private sealed class FakeArasCadClient : IArasCadClient
        {
            public int CancelCallCount { get; private set; }
            public bool CancelShouldSucceed { get; set; } = true;

            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
            {
                CancelCallCount++;
                if (!CancelShouldSucceed)
                    throw new System.InvalidOperationException("Authority unlock failed.");
                return Task.FromResult(new CancelCheckoutResult { Success = true });
            }

            public void Dispose() { }
            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct) => Task.FromResult<ArasLoginResult>(null);
            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct) => Task.FromResult<PartSearchResponse>(null);
            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct) => Task.FromResult<CreateCadResult>(null);
            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct) => Task.FromResult<CadCheckoutResult>(null);
            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct) => Task.FromResult<FileUploadResult>(null);
            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct) => Task.FromResult<CadCheckinResult>(null);
            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct) => Task.FromResult<string>(null);
            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct = default) => Task.FromResult<CadOperationContext>(null);
            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct = default) => Task.FromResult<CadOperationContext>(null);
            public Task<string> GetPrimaryCadIdForPartAsync(string partId, CancellationToken ct) => Task.FromResult<string>(null);
        }

        private sealed class StubRecoveryService : IRecoveryCopyService
        {
            public bool ShouldSucceed { get; set; } = true;
            public string BackupPath { get; set; } = Path.Combine(Path.GetTempPath(), "recovery", "cad.ics");

            public Task<RecoveryCopyResult> CreateRecoveryCopyAsync(string cadId, string workingFilePath, CancellationToken ct)
            {
                return Task.FromResult(new RecoveryCopyResult
                {
                    Succeeded = ShouldSucceed,
                    BackupPath = ShouldSucceed ? BackupPath : null,
                    ErrorMessage = ShouldSucceed ? null : "Recovery failed on purpose."
                });
            }

            public string GetRecoveryDirectory(string cadId) => Path.GetDirectoryName(BackupPath);
            public Task CleanExpiredCopiesAsync(CancellationToken ct) => Task.CompletedTask;
        }

        private static string ComputeHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var bytes = sha256.ComputeHash(stream);
                return System.BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
            }
        }

        private static PdmProjectsViewModel BuildWithManifest(
            FakeArasCadClient cadClient,
            out string folder,
            out string file,
            string fileContent,
            string baselineOverride = null)
        {
            folder = Path.Combine(Path.GetTempPath(), "pdm-cancel-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, "cad.ics");
            File.WriteAllText(file, fileContent);
            var baseline = baselineOverride ?? ComputeHash(file);

            MainViewModel.SharedArasCadClient = cadClient;
            MainViewModel.SharedUserName = "tester";

            var viewModel = new PdmProjectsViewModel { FolderPath = folder };
            var ws = new WorkspaceService(new WorkspaceOptions());
            ws.SaveManifest(new WorkspaceManifest
            {
                ProjectFolder = folder,
                CadId = "CAD1",
                LocalFilePath = file,
                LockedBy = "tester",
                CheckoutBaselineHash = baseline
            });
            return viewModel;
        }

        [Fact]
        public async Task AuthorityFailure_PreservesManifestAndLock()
        {
            var cadClient = new FakeArasCadClient { CancelShouldSucceed = false };
            var viewModel = BuildWithManifest(cadClient, out var folder, out var file, "baseline content");

            try
            {
                viewModel._checkoutService = new CheckoutService(
                    cadClient, new WorkspaceService(new WorkspaceOptions()), new StubRecoveryService());

                await viewModel.CancelCheckoutCoreAsync();

                var ws = new WorkspaceService(new WorkspaceOptions());
                Assert.NotNull(ws.LoadManifest(folder));
                Assert.Equal(1, cadClient.CancelCallCount);
            }
            finally
            {
                MainViewModel.SharedArasCadClient = null;
                MainViewModel.SharedUserName = null;
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public async Task RecoveryFailure_StopsBeforeAuthorityUnlock()
        {
            var cadClient = new FakeArasCadClient();
            var recovery = new StubRecoveryService { ShouldSucceed = false };
            var viewModel = BuildWithManifest(cadClient, out var folder, out var file, "baseline content");

            try
            {
                // Overwrite the file so it differs from the captured baseline (modified).
                File.WriteAllText(file, "changed content");

                viewModel._checkoutService = new CheckoutService(
                    cadClient, new WorkspaceService(new WorkspaceOptions()), recovery);

                await viewModel.CancelCheckoutCoreAsync();

                // Recovery failed -> authority unlock must NOT be attempted.
                Assert.Equal(0, cadClient.CancelCallCount);
                Assert.NotNull(new WorkspaceService(new WorkspaceOptions()).LoadManifest(folder));
            }
            finally
            {
                MainViewModel.SharedArasCadClient = null;
                MainViewModel.SharedUserName = null;
                Directory.Delete(folder, true);
            }
        }
    }
}
