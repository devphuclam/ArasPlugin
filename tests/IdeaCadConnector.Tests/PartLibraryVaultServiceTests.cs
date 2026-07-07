using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PartLibraryVaultServiceTests
    {
        [Fact]
        public void BuildCacheKey_IncludesAllComponents()
        {
            var service = CreateService();
            var key = service.BuildCacheKey("file-123", "2");

            Assert.Equal("file-123", key.FileId);
            Assert.Equal("2", key.RevisionGeneration);
            Assert.NotNull(key.Server);
            Assert.NotNull(key.Database);
        }

        [Fact]
        public void BuildCacheKey_NullGeneration_UsesDefault()
        {
            var service = CreateService();
            var key = service.BuildCacheKey("file-123", null);

            Assert.Equal("file-123", key.FileId);
            Assert.Equal("0", key.RevisionGeneration);
        }

        [Fact]
        public void ToCacheFileName_ProducesSafeFileName()
        {
            var key = new VaultCacheKey
            {
                Server = "http://server/",
                Database = "InnovatorSolutions",
                FileId = "file-123",
                RevisionGeneration = "2"
            };

            var name = key.ToCacheFileName();

            Assert.DoesNotContain("/", name);
            Assert.DoesNotContain(":", name);
            Assert.EndsWith(".cache", name);
        }

        [Fact]
        public void VaultCacheKey_Equality_ComparesAllFields()
        {
            var a = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1" };
            var b = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1" };
            var c = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "2" };

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void GetCachedFilePath_NullKey_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.GetCachedFilePath((VaultCacheKey)null));
        }

        [Fact]
        public void GetCachedFilePath_NullCadInfo_ReturnsNull()
        {
            var service = CreateService();
            Assert.Null(service.GetCachedFilePath((PartLibraryCadFileInfo)null));
        }

        [Fact]
        public void GetCachedFilePath_MissingFile_ReturnsNull()
        {
            var service = CreateService();
            var key = new VaultCacheKey { FileId = "nonexistent" };
            Assert.Null(service.GetCachedFilePath(key));
        }

        [Fact]
        public async Task DownloadToCacheAsync_NullCadInfo_ReturnsValidationFailed()
        {
            var service = CreateService();
            var result = await service.DownloadToCacheAsync(null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task DownloadToCacheAsync_NoNativeFile_ReturnsCadNotFound()
        {
            var service = CreateService();
            var info = new PartLibraryCadFileInfo { HasNative = false };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.CadNotFound, result.ErrorCode);
        }

        [Fact]
        public async Task DownloadToCacheAsync_NoFileId_ReturnsCadNotFound()
        {
            var service = CreateService();
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = null };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.CadNotFound, result.ErrorCode);
        }

        [Fact]
        public async Task DownloadToCacheAsync_ArasError_ReturnsResultWithErrorCode()
        {
            var faulted = new FaultedArasCadClient(new ArasOperationException(
                ArasErrorCode.PermissionDenied, "access denied"));
            var service = new PartLibraryVaultService(faulted);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f1", FileName = "test.ics" };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.PermissionDenied, result.ErrorCode);
        }

        [Fact]
        public async Task DownloadToCacheAsync_Cancellation_Propagates()
        {
            var service = CreateService();
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f1", FileName = "test.ics" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                service.DownloadToCacheAsync(info, cts.Token));
        }

        [Fact]
        public void CleanTempOnFailure_ExistingFile_Deletes()
        {
            var tempPath = Path.GetTempFileName();
            try
            {
                Assert.True(File.Exists(tempPath));
                var service = CreateService();
                service.CleanTempOnFailure(tempPath);
                Assert.False(File.Exists(tempPath));
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void CleanTempOnFailure_NullPath_DoesNotThrow()
        {
            var service = CreateService();
            service.CleanTempOnFailure(null);
        }

        [Fact]
        public void CleanTempOnFailure_NonExistentPath_DoesNotThrow()
        {
            var service = CreateService();
            service.CleanTempOnFailure(@"C:\does-not-exist\tmp.dat");
        }

        [Fact]
        public async Task DownloadToCacheAsync_ZeroByteDownload_ReturnsError()
        {
            var empty = new EmptyFileArasCadClient();
            var service = new PartLibraryVaultService(empty);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-empty", FileName = "empty.ics" };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DownloadToCacheAsync_AlreadyCached_ReturnsCachedPath()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-cache-test");
            var service = new PartLibraryVaultService(new StubArasCadClient(), cacheRoot);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-cached", FileName = "cached.ics", Generation = "1" };
            var key = service.BuildCacheKey("f-cached", "1");
            Directory.CreateDirectory(cacheRoot);
            var cachePath = Path.Combine(cacheRoot, key.ToCacheFileName());
            try
            {
                File.WriteAllText(cachePath, "cached-content");

                var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(cachePath, result.LocalFilePath);
            }
            finally
            {
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task DownloadToCacheAsync_SuccessfulDownload_MovesToCache()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-cache-success-test");
            var service = new PartLibraryVaultService(new StubArasCadClient(), cacheRoot);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-new", FileName = "new.ics", Generation = "1" };

            try
            {
                var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

                Assert.True(result.Success);
                Assert.NotNull(result.LocalFilePath);
                Assert.True(File.Exists(result.LocalFilePath));
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);
            }
        }

        private static PartLibraryVaultService CreateService()
        {
            return new PartLibraryVaultService(new StubArasCadClient());
        }

        private sealed class StubArasCadClient : IArasCadClient
        {
            public void Dispose() { }

            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                var path = Path.Combine(targetDirectory, $"downloaded_{fileId}.ics");
                File.WriteAllText(path, "test-content");
                return Task.FromResult(path);
            }

            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
                => Task.FromResult(new ArasLoginResult());

            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
                => Task.FromResult(new PartSearchResponse(Array.Empty<PartSearchResult>(), 0));

            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
                => Task.FromResult(new CreateCadResult());

            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
                => Task.FromResult(new FileUploadResult());

            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckinResult());

            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CancelCheckoutResult());

            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            private static CadOperationContext MakeEmptyContext()
                => new CadOperationContext(null, null, null, 0, null, null, false, false, null, null, null, null);
        }

        private sealed class FaultedArasCadClient : IArasCadClient
        {
            private readonly ArasOperationException _exception;

            public FaultedArasCadClient(ArasOperationException exception)
            {
                _exception = exception;
            }

            public void Dispose() { }

            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
            {
                throw _exception;
            }

            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
                => Task.FromResult(new ArasLoginResult());

            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
                => Task.FromResult(new PartSearchResponse(Array.Empty<PartSearchResult>(), 0));

            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
                => Task.FromResult(new CreateCadResult());

            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
                => Task.FromResult(new FileUploadResult());

            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckinResult());

            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CancelCheckoutResult());

            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            private static CadOperationContext MakeEmptyContext()
                => new CadOperationContext(null, null, null, 0, null, null, false, false, null, null, null, null);
        }

        private sealed class EmptyFileArasCadClient : IArasCadClient
        {
            public void Dispose() { }

            public Task<string> DownloadNativeFileAsync(string fileId, string targetDirectory, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                Directory.CreateDirectory(targetDirectory);
                var path = Path.Combine(targetDirectory, "empty.ics");
                File.WriteAllText(path, "");
                return Task.FromResult(path);
            }

            public Task<ArasLoginResult> LoginAsync(ArasLoginRequest request, CancellationToken ct)
                => Task.FromResult(new ArasLoginResult());

            public Task<PartSearchResponse> SearchPartsAsync(PartSearchRequest request, CancellationToken ct)
                => Task.FromResult(new PartSearchResponse(Array.Empty<PartSearchResult>(), 0));

            public Task<CreateCadResult> CreateCadAsync(CreateCadRequest request, CancellationToken ct)
                => Task.FromResult(new CreateCadResult());

            public Task<CadCheckoutResult> CheckoutAsync(CadCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<CadCheckoutResult> OpenReadOnlyAsync(CadOpenReadOnlyRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckoutResult());

            public Task<FileUploadResult> UploadFileAsync(FileUploadRequest request, CancellationToken ct)
                => Task.FromResult(new FileUploadResult());

            public Task<CadCheckinResult> CheckinAsync(CadCheckinRequest request, CancellationToken ct)
                => Task.FromResult(new CadCheckinResult());

            public Task<CancelCheckoutResult> CancelCheckoutAsync(CancelCheckoutRequest request, CancellationToken ct)
                => Task.FromResult(new CancelCheckoutResult());

            public Task<CadOperationContext> GetCadOperationContextAsync(string cadId, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            public Task<CadOperationContext> ExecuteCadBusinessActionAsync(ExecuteCadBusinessActionRequest request, CancellationToken ct)
                => Task.FromResult(MakeEmptyContext());

            private static CadOperationContext MakeEmptyContext()
                => new CadOperationContext(null, null, null, 0, null, null, false, false, null, null, null, null);
        }
    }
}
