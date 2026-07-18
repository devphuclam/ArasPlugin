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
    public sealed class CheckoutServiceCancelRecoveryTests
    {
        private sealed class StubArasCadClient : IArasCadClient
        {
            public bool CancelCalled { get; private set; }

            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
            {
                CancelCalled = true;
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
        }

        private sealed class StubRecoveryService : IRecoveryCopyService
        {
            public bool ShouldSucceed { get; set; } = true;
            public string BackupPath { get; set; } = "C:\\recovery\\cad.ics";
            public bool CreateCalled { get; private set; }

            public Task<RecoveryCopyResult> CreateRecoveryCopyAsync(string cadId, string workingFilePath, CancellationToken ct)
            {
                CreateCalled = true;
                return Task.FromResult(new RecoveryCopyResult
                {
                    Succeeded = ShouldSucceed,
                    BackupPath = ShouldSucceed ? BackupPath : null,
                    ErrorMessage = ShouldSucceed ? null : "Recovery failed on purpose."
                });
            }

            public string GetRecoveryDirectory(string cadId) => "C:\\recovery";
            public Task CleanExpiredCopiesAsync(CancellationToken ct) => Task.CompletedTask;
        }

        private static CheckoutService BuildService(StubArasCadClient cadClient, StubRecoveryService recovery)
        {
            var ws = new WorkspaceService(new WorkspaceOptions());
            return new CheckoutService(cadClient, ws, recovery);
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

        [Fact]
        public async Task Prepare_MissingFile_ReturnsNotModified_NoRecovery()
        {
            var cadClient = new StubArasCadClient();
            var recovery = new StubRecoveryService();
            var service = BuildService(cadClient, recovery);

            var result = await service.PrepareCancelCheckoutAsync("CAD1", null, "ignored", CancellationToken.None);

            Assert.False(result.FileWasModified);
            Assert.Null(result.ErrorMessage);
            Assert.False(recovery.CreateCalled);
        }

        [Fact]
        public async Task Prepare_UnchangedFile_NoRecoveryNeeded_ReturnsNotModified()
        {
            var cadClient = new StubArasCadClient();
            var recovery = new StubRecoveryService();
            var service = BuildService(cadClient, recovery);

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "baseline content");
                var baseline = ComputeHash(path);

                var result = await service.PrepareCancelCheckoutAsync("CAD1", path, baseline, CancellationToken.None);

                Assert.False(result.FileWasModified);
                Assert.Null(result.RecoveryPath);
                Assert.Null(result.ErrorMessage);
                Assert.False(recovery.CreateCalled);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Prepare_ModifiedFile_CreatesRecoveryCopy()
        {
            var cadClient = new StubArasCadClient();
            var recovery = new StubRecoveryService { ShouldSucceed = true };
            var service = BuildService(cadClient, recovery);

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "baseline content");
                var baseline = ComputeHash(path);
                File.WriteAllText(path, "modified content");

                var result = await service.PrepareCancelCheckoutAsync("CAD1", path, baseline, CancellationToken.None);

                Assert.True(result.FileWasModified);
                Assert.Equal(recovery.BackupPath, result.RecoveryPath);
                Assert.True(recovery.CreateCalled);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Prepare_RecoveryFails_ReturnsErrorMessage()
        {
            var cadClient = new StubArasCadClient();
            var recovery = new StubRecoveryService { ShouldSucceed = false };
            var service = BuildService(cadClient, recovery);

            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "baseline content");
                var baseline = ComputeHash(path);
                File.WriteAllText(path, "modified content");

                var result = await service.PrepareCancelCheckoutAsync("CAD1", path, baseline, CancellationToken.None);

                Assert.True(result.FileWasModified);
                Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task CancelCheckout_ReleasesLock()
        {
            var cadClient = new StubArasCadClient();
            var recovery = new StubRecoveryService();
            var service = BuildService(cadClient, recovery);

            var success = await service.CancelCheckoutAsync("CAD1", CancellationToken.None);

            Assert.True(success);
            Assert.True(cadClient.CancelCalled);
        }
    }
}
