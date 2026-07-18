using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Workspace;
using IdeaCadConnector.Workspace.Recovery;

namespace IdeaCadConnector.Desktop
{
    public sealed class CheckoutResultInfo
    {
        public bool Success { get; set; }
        public string LockToken { get; set; }
        public string LocalFilePath { get; set; }
        public CadSummary Cad { get; set; }
        public string CadId { get; set; }
        public string ErrorMessage { get; set; }
        public string NativeFileId { get; set; }
        public bool IsReadOnly { get; set; }
        /// <summary>SHA256 of the downloaded file content captured at checkout time.</summary>
        public string CheckoutBaselineHash { get; set; }
    }

    public sealed class CheckinResultInfo
    {
        public bool Success { get; set; }
        public CadSummary Cad { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class CancelCheckoutRecoveryInfo
    {
        public bool FileWasModified { get; set; }
        public string RecoveryPath { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class CheckoutService
    {
        private readonly IArasCadClient _arasClient;
        private readonly IRecoveryCopyService _recoveryService;
        public CheckoutService(
            IArasCadClient arasClient,
            WorkspaceService workspaceService,
            IRecoveryCopyService recoveryService = null)
        {
            _arasClient = arasClient ?? throw new ArgumentNullException(nameof(arasClient));
            if (workspaceService == null)
                throw new ArgumentNullException(nameof(workspaceService));
            _recoveryService = recoveryService;
        }

        public async Task<CheckoutResultInfo> CheckoutAndDownloadAsync(
            string cadId,
            string targetDirectory,
            CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);
                var result = await _arasClient.CheckoutAsync(
                    new CadCheckoutRequest { CadId = cadId, PartId = null },
                    ct);

                var localFilePath = Path.Combine(targetDirectory, $"{result.Cad?.CadNumber ?? "part"}.ics");
                if (result.Cad != null && result.Cad.HasNativeFile)
                {
                    localFilePath = await _arasClient.DownloadNativeFileAsync(
                        result.Cad.NativeFileId, targetDirectory, ct);
                }
                else
                {
                    File.WriteAllBytes(localFilePath, Array.Empty<byte>());
                }

                var baselineHash = await ComputeSha256Async(localFilePath, ct);

                return new CheckoutResultInfo
                {
                    Success = true,
                    LockToken = result.LockToken,
                    LocalFilePath = localFilePath,
                    Cad = result.Cad,
                    CadId = cadId,
                    NativeFileId = result.Cad?.NativeFileId,
                    CheckoutBaselineHash = baselineHash
                };
            }
            catch (Exception ex)
            {
                return new CheckoutResultInfo
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<CheckoutResultInfo> OpenReadOnlyAsync(
            string cadId,
            string targetDirectory,
            CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(targetDirectory);
                var result = await _arasClient.OpenReadOnlyAsync(
                    new CadOpenReadOnlyRequest { CadId = cadId, PartId = null },
                    ct);

                if (result.Cad == null || !result.Cad.HasNativeFile)
                {
                    return new CheckoutResultInfo
                    {
                        Success = false,
                        ErrorMessage = LocalizationSource.Instance[TranslationKeys.CheckoutErrorNoNativeFile],
                        CadId = cadId,
                        IsReadOnly = true
                    };
                }

                var localFilePath = await _arasClient.DownloadNativeFileAsync(
                    result.Cad.NativeFileId, targetDirectory, ct);

                var baselineHash = await ComputeSha256Async(localFilePath, ct);

                return new CheckoutResultInfo
                {
                    Success = true,
                    LocalFilePath = localFilePath,
                    Cad = result.Cad,
                    CadId = cadId,
                    NativeFileId = result.Cad.NativeFileId,
                    IsReadOnly = true,
                    CheckoutBaselineHash = baselineHash
                };
            }
            catch (Exception ex)
            {
                return new CheckoutResultInfo
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    CadId = cadId,
                    IsReadOnly = true
                };
            }
        }

        public async Task<CheckinResultInfo> UploadAndCheckinAsync(
            string cadId,
            string lockToken,
            string localFilePath,
            CadMetadata metadata,
            string reason,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
                {
                    return new CheckinResultInfo
                    {
                        Success = false,
                        ErrorMessage = LocalizationSource.Instance[TranslationKeys.CheckoutErrorLocalFileNotFound]
                    };
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return new CheckinResultInfo
                    {
                        Success = false,
                        ErrorMessage = "A written reason is required before check-in."
                    };
                }

                var uploadResult = await _arasClient.UploadFileAsync(new FileUploadRequest
                {
                    FilePath = localFilePath,
                    FileName = Path.GetFileName(localFilePath)
                }, ct);

                var request = CadCheckinRequest.CreateNew();
                request.CadId = cadId;
                request.LockToken = lockToken;
                request.UploadedFileId = uploadResult.UploadedFileId;
                request.LocalFilePath = localFilePath;
                request.Metadata = metadata ?? new CadMetadata();
                request.Comment = reason;

                var checkinResult = await _arasClient.CheckinAsync(request, ct);

                return new CheckinResultInfo
                {
                    Success = true,
                    Cad = checkinResult.Cad
                };
            }
            catch (Exception ex)
            {
                return new CheckinResultInfo
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> CancelCheckoutAsync(string cadId, CancellationToken ct)
        {
            try
            {
                await _arasClient.CancelCheckoutAsync(
                    new CancelCheckoutRequest { CadId = cadId },
                    ct);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<CancelCheckoutRecoveryInfo> PrepareCancelCheckoutAsync(
            string cadId,
            string localFilePath,
            string baselineHash,
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cadId))
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = false,
                        ErrorMessage = "CAD ID must not be null or empty."
                    };
                }

                if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = false
                    };
                }

                string sourceHash;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(localFilePath))
                {
                    ct.ThrowIfCancellationRequested();
                    var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), ct);
                    sourceHash = BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
                }

                if (string.IsNullOrWhiteSpace(sourceHash))
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = false
                    };
                }

                bool changed;
                if (string.IsNullOrWhiteSpace(baselineHash))
                {
                    // No verified baseline is available. Be conservative and treat the
                    // working copy as modified so recovery/confirmation is required and
                    // nothing is silently deleted.
                    changed = true;
                }
                else
                {
                    changed = !string.Equals(sourceHash, baselineHash, StringComparison.OrdinalIgnoreCase);
                }

                if (!changed)
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = false
                    };
                }

                if (_recoveryService == null)
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = true,
                        ErrorMessage = "Recovery service is not available."
                    };
                }

                var recoveryResult = await _recoveryService.CreateRecoveryCopyAsync(
                    cadId, localFilePath, ct);

                if (!recoveryResult.Succeeded)
                {
                    return new CancelCheckoutRecoveryInfo
                    {
                        FileWasModified = true,
                        ErrorMessage = recoveryResult.ErrorMessage ?? "Recovery copy failed."
                    };
                }

                return new CancelCheckoutRecoveryInfo
                {
                    FileWasModified = true,
                    RecoveryPath = recoveryResult.BackupPath
                };
            }
            catch (OperationCanceledException)
            {
                return new CancelCheckoutRecoveryInfo
                {
                    FileWasModified = false,
                    ErrorMessage = "Cancel checkout was cancelled."
                };
            }
            catch (Exception ex)
            {
                return new CancelCheckoutRecoveryInfo
                {
                    FileWasModified = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                ct.ThrowIfCancellationRequested();
                var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), ct);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToUpperInvariant();
            }
        }
    }
}
