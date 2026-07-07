using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class PartLibraryVaultService : IPartLibraryVaultService
    {
        private readonly IArasCadClient _arasCadClient;
        private readonly IPartLibraryClient _partLibraryClient;
        private readonly string _serverUrl;
        private readonly string _database;
        private readonly string _cacheRoot;

        public PartLibraryVaultService(
            IArasCadClient arasCadClient,
            string cacheRoot = null,
            IPartLibraryClient partLibraryClient = null,
            string serverUrl = null,
            string database = null)
        {
            _arasCadClient = arasCadClient ?? throw new ArgumentNullException(nameof(arasCadClient));
            _partLibraryClient = partLibraryClient;
            _serverUrl = serverUrl ?? "default";
            _database = database ?? "default";
            _cacheRoot = cacheRoot ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdeaCadConnector",
                "vault-cache");
        }

        public async Task<PartLibraryCadFileInfo> GetPrimaryCadFileInfoAsync(
            string entryId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "Entry ID is required to resolve primary CAD.");

            if (_partLibraryClient == null)
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "IPartLibraryClient is not available. Cannot resolve entry details.");

            var entry = await _partLibraryClient.GetEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
            if (entry == null)
                throw new ArasOperationException(
                    ArasErrorCode.CadNotFound,
                    $"Library entry '{entryId}' not found.");

            if (string.IsNullOrWhiteSpace(entry.PrimaryCadId))
                throw new ArasOperationException(
                    ArasErrorCode.CadNotFound,
                    $"Library entry '{entryId}' has no primary CAD associated.");

            return new PartLibraryCadFileInfo
            {
                CadId = entry.PrimaryCadId,
                CadName = entry.PrimaryCadFileName,
                FileName = entry.PrimaryCadFileName,
                CadState = entry.PrimaryCadState,
                LockedBy = entry.LockedBy,
                HasNative = !string.IsNullOrWhiteSpace(entry.PrimaryCadId)
            };
        }

        public async Task<VaultDownloadResult> DownloadToCacheAsync(
            PartLibraryCadFileInfo cadFileInfo,
            CancellationToken cancellationToken)
        {
            if (cadFileInfo == null)
                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = "CAD file info is required.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };

            if (!cadFileInfo.HasNative || string.IsNullOrWhiteSpace(cadFileInfo.FileId))
                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = "No native file available for this CAD.",
                    ErrorCode = ArasErrorCode.CadNotFound
                };

            // D-05: Validate file name, extension, and path traversal
            var fileName = cadFileInfo.FileName ?? $"file_{cadFileInfo.FileId}.ics";
            if (!VaultFileValidator.IsValidFileName(fileName))
            {
                if (VaultFileValidator.ContainsPathTraversal(fileName))
                    return new VaultDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid file name: path traversal detected.",
                        ErrorCode = ArasErrorCode.ValidationFailed
                    };

                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = $"File extension '{Path.GetExtension(fileName)}' is not in the approved list.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };
            }

            var cacheKey = BuildCacheKey(cadFileInfo.FileId, cadFileInfo.Generation, fileName, null);

            // Cache hit validation: exists, readable, size > 0, approved extension
            var cached = GetCachedFilePath(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                try
                {
                    var fi = new FileInfo(cached);
                    if (fi.Exists && fi.Length > 0)
                    {
                        var cachedExt = VaultFileValidator.GetNormalizedExtension(fi.Name);
                        if (VaultFileValidator.IsExtensionAllowed(cachedExt))
                        {
                            return new VaultDownloadResult
                            {
                                Success = true,
                                LocalFilePath = cached,
                                FileId = cadFileInfo.FileId,
                                FileName = fileName,
                                BytesWritten = fi.Length,
                                FromCache = true,
                                CacheKey = cacheKey
                            };
                        }
                    }
                }
                catch
                {
                }
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-download");
            Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, fileName);
            try
            {
                var downloaded = await _arasCadClient.DownloadNativeFileAsync(
                    cadFileInfo.FileId,
                    tempDir,
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(downloaded))
                {
                    CleanTempOnFailure(tempPath);
                    return new VaultDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Download returned no file path.",
                        ErrorCode = ArasErrorCode.FileUploadNotFound,
                        FileId = cadFileInfo.FileId,
                        FileName = fileName,
                        CacheKey = cacheKey
                    };
                }

                var fileInfo = new FileInfo(downloaded);
                if (fileInfo.Length == 0)
                {
                    CleanTempOnFailure(downloaded);
                    return new VaultDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Downloaded file is empty (zero bytes).",
                        ErrorCode = ArasErrorCode.FileUploadNotFound,
                        FileId = cadFileInfo.FileId,
                        FileName = fileName,
                        CacheKey = cacheKey
                    };
                }

                var downloadedExt = VaultFileValidator.GetNormalizedExtension(downloaded);
                if (!VaultFileValidator.IsExtensionAllowed(downloadedExt))
                {
                    CleanTempOnFailure(downloaded);
                    return new VaultDownloadResult
                    {
                        Success = false,
                        ErrorMessage = "Downloaded file has an invalid extension.",
                        ErrorCode = ArasErrorCode.ValidationFailed,
                        FileId = cadFileInfo.FileId,
                        FileName = fileName,
                        CacheKey = cacheKey
                    };
                }

                var cacheDir = GetCacheDirectory();
                Directory.CreateDirectory(cacheDir);
                var cacheFileName = cacheKey.ToCacheFileName();
                var cachePath = Path.Combine(cacheDir, cacheFileName);

                // Atomic move: copy to temp then rename
                var tempCachePath = cachePath + ".tmp";
                if (File.Exists(tempCachePath))
                    File.Delete(tempCachePath);
                File.Copy(downloaded, tempCachePath);

                if (File.Exists(cachePath))
                    File.Delete(cachePath);

                if (File.Exists(tempCachePath))
                {
                    File.Move(tempCachePath, cachePath);
                    CleanTempOnFailure(downloaded);
                }

                return new VaultDownloadResult
                {
                    Success = true,
                    LocalFilePath = cachePath,
                    FileId = cadFileInfo.FileId,
                    FileName = fileName,
                    BytesWritten = fileInfo.Length,
                    FromCache = false,
                    CacheKey = cacheKey
                };
            }
            catch (OperationCanceledException)
            {
                CleanTempOnFailure(tempPath);
                throw;
            }
            catch (ArasOperationException aex)
            {
                CleanTempOnFailure(tempPath);
                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = aex.Message,
                    ErrorCode = aex.ErrorCode,
                    FileId = cadFileInfo.FileId,
                    FileName = fileName,
                    CacheKey = cacheKey
                };
            }
            catch (Exception ex)
            {
                CleanTempOnFailure(tempPath);
                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = $"Download failed: {ex.Message}",
                    ErrorCode = ArasErrorCode.UnexpectedServerError,
                    FileId = cadFileInfo.FileId,
                    FileName = fileName,
                    CacheKey = cacheKey
                };
            }
        }

        public string GetCachedFilePath(PartLibraryCadFileInfo cadFileInfo)
        {
            if (cadFileInfo == null || string.IsNullOrWhiteSpace(cadFileInfo.FileId))
                return null;

            return GetCachedFilePath(BuildCacheKey(cadFileInfo.FileId, cadFileInfo.Generation, cadFileInfo.FileName, null));
        }

        public string GetCachedFilePath(VaultCacheKey cacheKey)
        {
            if (cacheKey == null)
                return null;

            var cacheDir = GetCacheDirectory();
            var fileName = cacheKey.ToCacheFileName();
            var path = Path.Combine(cacheDir, fileName);
            return File.Exists(path) ? path : null;
        }

        public VaultCacheKey BuildCacheKey(string fileId, string revisionGeneration, string fileName, string userName)
        {
            var ext = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : null;
            return new VaultCacheKey
            {
                Server = _serverUrl,
                Database = _database,
                FileId = fileId,
                RevisionGeneration = revisionGeneration ?? "0",
                UserName = userName,
                FileName = fileName,
                Extension = ext
            };
        }

        public void CleanTempOnFailure(string tempPath)
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        private string GetCacheDirectory()
        {
            return _cacheRoot;
        }
    }
}
