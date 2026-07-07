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
        private readonly string _cacheRoot;

        public PartLibraryVaultService(IArasCadClient arasCadClient, string cacheRoot = null)
        {
            _arasCadClient = arasCadClient ?? throw new ArgumentNullException(nameof(arasCadClient));
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

            // CAD file info is returned as part of PartLibraryEntryDetails.
            // This service method would need an injected IPartLibraryClient or the details
            // would be pre-loaded by the caller. For now, the method signature exists
            // so the VM can pass already-loaded PartLibraryEntryDetails data.
            // Full AML-backed resolution lives in the Aras project.
            throw new NotSupportedException(
                "Use an IPartLibraryClient to load entry details, then pass the data to DownloadToCacheAsync.");
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

            var cacheKey = BuildCacheKey(cadFileInfo.FileId, cadFileInfo.Generation);
            var cached = GetCachedFilePath(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached) && File.Exists(cached))
            {
                return new VaultDownloadResult
                {
                    Success = true,
                    LocalFilePath = cached
                };
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "IdeaCadConnector", "vault-download");
            Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, cadFileInfo.FileName ?? $"file_{cadFileInfo.FileId}.ics");
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
                        ErrorCode = ArasErrorCode.FileUploadNotFound
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
                        ErrorCode = ArasErrorCode.FileUploadNotFound
                    };
                }

                var cacheDir = GetCacheDirectory();
                Directory.CreateDirectory(cacheDir);
                var cacheFileName = cacheKey.ToCacheFileName();
                var cachePath = Path.Combine(cacheDir, cacheFileName);
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                File.Move(downloaded, cachePath);

                return new VaultDownloadResult
                {
                    Success = true,
                    LocalFilePath = cachePath
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
                    ErrorCode = aex.ErrorCode
                };
            }
            catch (Exception ex)
            {
                CleanTempOnFailure(tempPath);
                return new VaultDownloadResult
                {
                    Success = false,
                    ErrorMessage = $"Download failed: {ex.Message}",
                    ErrorCode = ArasErrorCode.UnexpectedServerError
                };
            }
        }

        public string GetCachedFilePath(PartLibraryCadFileInfo cadFileInfo)
        {
            if (cadFileInfo == null || string.IsNullOrWhiteSpace(cadFileInfo.FileId))
                return null;

            return GetCachedFilePath(BuildCacheKey(cadFileInfo.FileId, cadFileInfo.Generation));
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

        public VaultCacheKey BuildCacheKey(string fileId, string revisionGeneration)
        {
            return new VaultCacheKey
            {
                Server = "default",
                Database = "default",
                FileId = fileId,
                RevisionGeneration = revisionGeneration ?? "0"
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
