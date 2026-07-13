using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Dto.Library;
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
            var key = service.BuildCacheKey("file-123", "2", "test.ics", "user1");

            Assert.Equal("file-123", key.FileId);
            Assert.Equal("2", key.RevisionGeneration);
            Assert.Equal("user1", key.UserName);
            Assert.Equal("test.ics", key.FileName);
            Assert.Equal(".ics", key.Extension);
            Assert.Equal("test-server", key.Server);
            Assert.Equal("test-db", key.Database);
        }

        [Fact]
        public void BuildCacheKey_NullGeneration_UsesDefault()
        {
            var service = CreateService();
            var key = service.BuildCacheKey("file-123", null, "test.ics", null);

            Assert.Equal("file-123", key.FileId);
            Assert.Equal("0", key.RevisionGeneration);
            Assert.Equal(".ics", key.Extension);
        }

        [Fact]
        public void BuildCacheKey_NullFileName_NullExtension()
        {
            var service = CreateService();
            var key = service.BuildCacheKey("file-123", "1", null, null);

            Assert.Null(key.Extension);
        }

        [Fact]
        public void ToCacheFileName_ProducesSafeFileNameWithApprovedExtension()
        {
            var key = new VaultCacheKey
            {
                Server = "http://server/",
                Database = "SampleDatabase",
                FileId = "file-123",
                RevisionGeneration = "2",
                Extension = ".ics"
            };

            var name = key.ToCacheFileName();

            Assert.DoesNotContain("/", name);
            Assert.DoesNotContain(":", name);
            Assert.EndsWith(".ics", name);
            Assert.DoesNotContain(".cache", name);
        }

        [Fact]
        public void ToCacheFileName_NullExtension_FallsBackToDotCache()
        {
            var key = new VaultCacheKey
            {
                Server = "srv",
                Database = "db",
                FileId = "f1",
                RevisionGeneration = "1"
            };

            var name = key.ToCacheFileName();

            Assert.EndsWith(".cache", name);
        }

        [Fact]
        public void ToCacheFileName_MissingDot_PrependsDot()
        {
            var key = new VaultCacheKey
            {
                Server = "srv",
                Database = "db",
                FileId = "f1",
                RevisionGeneration = "1",
                Extension = "ics"
            };

            var name = key.ToCacheFileName();

            Assert.EndsWith(".ics", name);
        }

        [Fact]
        public void VaultCacheKey_Equality_ComparesAllFields()
        {
            var a = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1", UserName = "u1", Extension = ".ics" };
            var b = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1", UserName = "u1", Extension = ".ics" };
            var c = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "2", UserName = "u1", Extension = ".ics" };
            var d = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1", UserName = "u2", Extension = ".ics" };

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, c);
            Assert.NotEqual(a, d);
        }

        [Fact]
        public void VaultCacheKey_Equality_ExcludesFileNameFromEquality()
        {
            var a = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1", UserName = "u1", Extension = ".ics" };
            var b = new VaultCacheKey { Server = "s1", Database = "d1", FileId = "f1", RevisionGeneration = "1", UserName = "u1", Extension = ".ics", FileName = "different.ics" };

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
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
            Assert.Null(result.FileId);
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
            Assert.Equal("f-empty", result.FileId);
            Assert.Equal("empty.ics", result.FileName);
            Assert.NotNull(result.CacheKey);
        }

        [Fact]
        public async Task DownloadToCacheAsync_AlreadyCached_ReturnsCachedPath()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-cache-test");
            var service = new PartLibraryVaultService(new StubArasCadClient(), cacheRoot);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-cached", FileName = "cached.ics", Generation = "1" };
            var key = service.BuildCacheKey("f-cached", "1", "cached.ics", null);
            Directory.CreateDirectory(cacheRoot);
            var cachePath = Path.Combine(cacheRoot, key.ToCacheFileName());
            try
            {
                File.WriteAllText(cachePath, "cached-content");
                var fi = new FileInfo(cachePath);

                var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal(cachePath, result.LocalFilePath);
                Assert.True(result.FromCache);
                Assert.Equal("f-cached", result.FileId);
                Assert.Equal("cached.ics", result.FileName);
                Assert.Equal(fi.Length, result.BytesWritten);
                Assert.NotNull(result.CacheKey);
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
                Assert.False(result.FromCache);
                Assert.Equal("f-new", result.FileId);
                Assert.Equal("new.ics", result.FileName);
                Assert.True(result.BytesWritten > 0);
                Assert.NotNull(result.CacheKey);
                Assert.Equal(".ics", result.CacheKey.Extension);
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);
            }
        }

        [Fact]
        public async Task DownloadToCacheAsync_UnapprovedExtension_ReturnsValidationFailed()
        {
            var service = CreateService();
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-bad", FileName = "bad.exe" };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("extension", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DownloadToCacheAsync_PathTraversalFileName_ReturnsValidationFailed()
        {
            var service = CreateService();
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-trav", FileName = "../../malicious.ics" };

            var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("traversal", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DownloadToCacheAsync_NoFileName_UsesDefault()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-cache-defaultname-test");
            var service = new PartLibraryVaultService(new StubArasCadClient(), cacheRoot);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-default", FileName = null, Generation = "1" };

            try
            {
                var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

                Assert.True(result.Success);
                Assert.NotNull(result.FileName);
                Assert.Contains("f-default", result.FileName);
                Assert.NotNull(result.CacheKey);
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);
            }
        }

        [Fact]
        public async Task DownloadToCacheAsync_Success_ReturnsPopulatedResult()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-cache-populated-test");
            var service = new PartLibraryVaultService(new StubArasCadClient(), cacheRoot);
            var info = new PartLibraryCadFileInfo { HasNative = true, FileId = "f-pop", FileName = "populated.ics", Generation = "2" };

            try
            {
                var result = await service.DownloadToCacheAsync(info, CancellationToken.None);

                Assert.True(result.Success);
                Assert.Equal("f-pop", result.FileId);
                Assert.Equal("populated.ics", result.FileName);
                Assert.True(result.BytesWritten > 0);
                Assert.False(result.FromCache);
                Assert.NotNull(result.CacheKey);
                Assert.Equal(".ics", result.CacheKey.Extension);
                Assert.Equal("f-pop", result.CacheKey.FileId);
                Assert.Equal("2", result.CacheKey.RevisionGeneration);
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);
            }
        }

        [Fact]
        public void BuildCacheKey_ServerAndDatabase_FromConstructor()
        {
            var service = new PartLibraryVaultService(
                new StubArasCadClient(),
                serverUrl: "http://my-aras/InnovatorServer",
                database: "MyDB");
            var key = service.BuildCacheKey("f1", "1", "test.ics", null);

            Assert.Equal("http://my-aras/InnovatorServer", key.Server);
            Assert.Equal("MyDB", key.Database);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_NullEntryId_ThrowsValidationFailed()
        {
            var service = CreateService();
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.GetPrimaryCadFileInfoAsync(null, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_EmptyEntryId_ThrowsValidationFailed()
        {
            var service = CreateService();
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.GetPrimaryCadFileInfoAsync("", CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_NoClient_ThrowsValidationFailed()
        {
            var service = CreateService();
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.GetPrimaryCadFileInfoAsync("entry-1", CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.Contains("IPartLibraryClient", ex.Message);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_WithClient_MapsEntryToCadInfo()
        {
            var stubClient = new StubPartLibraryClient();
            stubClient.EntryToReturn = new PartLibraryEntryDetails
            {
                EntryId = "entry-1",
                PrimaryCadId = "cad-123",
                PrimaryCadFileName = "test.ics",
                PrimaryCadFileId = "file-456",
                PrimaryCadState = "Released",
                LockedBy = "user1"
            };

            var service = new PartLibraryVaultService(
                new StubArasCadClient(),
                partLibraryClient: stubClient,
                serverUrl: "srv",
                database: "db");

            var result = await service.GetPrimaryCadFileInfoAsync("entry-1", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("cad-123", result.CadId);
            Assert.Equal("test.ics", result.CadName);
            Assert.Equal("test.ics", result.FileName);
            Assert.Equal("Released", result.CadState);
            Assert.Equal("user1", result.LockedBy);
            Assert.True(result.HasNative);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_EntryNotFound_ThrowsCadNotFound()
        {
            var stubClient = new StubPartLibraryClient();
            stubClient.EntryToReturn = null;

            var service = new PartLibraryVaultService(
                new StubArasCadClient(),
                partLibraryClient: stubClient,
                serverUrl: "srv",
                database: "db");

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.GetPrimaryCadFileInfoAsync("entry-1", CancellationToken.None));

            Assert.Equal(ArasErrorCode.CadNotFound, ex.ErrorCode);
        }

        [Fact]
        public async Task GetPrimaryCadFileInfoAsync_NoCadId_ThrowsCadNotFound()
        {
            var stubClient = new StubPartLibraryClient();
            stubClient.EntryToReturn = new PartLibraryEntryDetails
            {
                EntryId = "entry-1",
                PrimaryCadId = null
            };

            var service = new PartLibraryVaultService(
                new StubArasCadClient(),
                partLibraryClient: stubClient,
                serverUrl: "srv",
                database: "db");

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                service.GetPrimaryCadFileInfoAsync("entry-1", CancellationToken.None));

            Assert.Equal(ArasErrorCode.CadNotFound, ex.ErrorCode);
        }

        private static PartLibraryVaultService CreateService()
        {
            return new PartLibraryVaultService(
                new StubArasCadClient(),
                serverUrl: "test-server",
                database: "test-db");
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

        private sealed class StubPartLibraryClient : IPartLibraryClient
        {
            public PartLibraryEntryDetails EntryToReturn { get; set; } = new PartLibraryEntryDetails();

            public void Dispose() { }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter, CancellationToken ct)
                => Task.FromResult((IReadOnlyList<PartLibrarySummary>)Array.Empty<PartLibrarySummary>());

            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken ct)
                => Task.FromResult(new LibraryMutationResult());

            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken ct)
                => Task.FromResult(new LibraryMutationResult());

            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken ct)
                => Task.FromResult(new LibraryMutationResult());

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken ct)
                => Task.FromResult(new MoveLibraryEntryResult());

            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken ct)
                => Task.FromResult(new PartPickerSearchResponse());

            public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken ct)
                => Task.FromResult(new PartRevisionHistoryResponse());

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken ct)
                => Task.FromResult(new PartPreview());

            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken ct)
                => Task.FromResult(new DuplicateEntryCheckResult());

            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken ct)
                => Task.FromResult(new PartLibrarySearchResponse());

            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken ct)
                => Task.FromResult(EntryToReturn);

            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken ct)
                => Task.FromResult(new AddPartToLibraryResult());

            public Task RemoveEntryAsync(string entryId, CancellationToken ct)
                => Task.FromResult(0);

            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken ct)
                => Task.FromResult(0);

            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken ct)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken ct)
                => Task.FromResult(new UpdateLibraryRevisionPolicyResult());

            public Task PublishEntryAsync(string entryId, CancellationToken ct)
                => Task.FromResult(0);

            public Task DeprecateEntryAsync(string entryId, CancellationToken ct)
                => Task.FromResult(0);

            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken ct)
                => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());

            public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken ct)
                => Task.FromResult(new RecordLibraryUsageResult());

            public Task<LibraryEntryCadDetails> GetCadDetailsAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new LibraryEntryCadDetails());

            public Task<LibraryEntryBomDetails> GetBomDetailsAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new LibraryEntryBomDetails());

            public Task<LibraryEntryRevisionDetails> GetRevisionDetailsAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new LibraryEntryRevisionDetails());

            public Task<LibraryEntryWhereUsedDetails> GetWhereUsedDetailsAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new LibraryEntryWhereUsedDetails());

            public Task<LibraryEntryDetailBundle> GetDetailBundleAsync(string entryId, CancellationToken ct)
                => Task.FromResult(new LibraryEntryDetailBundle());
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
