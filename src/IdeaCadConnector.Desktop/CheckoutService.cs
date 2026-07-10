using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Workspace;

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
    }

    public sealed class CheckinResultInfo
    {
        public bool Success { get; set; }
        public CadSummary Cad { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class CheckoutService
    {
        private readonly IArasCadClient _arasClient;
        public CheckoutService(IArasCadClient arasClient, WorkspaceService workspaceService)
        {
            _arasClient = arasClient ?? throw new ArgumentNullException(nameof(arasClient));
            if (workspaceService == null)
                throw new ArgumentNullException(nameof(workspaceService));
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

                return new CheckoutResultInfo
                {
                    Success = true,
                    LockToken = result.LockToken,
                    LocalFilePath = localFilePath,
                    Cad = result.Cad,
                    CadId = cadId,
                    NativeFileId = result.Cad?.NativeFileId
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

                return new CheckoutResultInfo
                {
                    Success = true,
                    LocalFilePath = localFilePath,
                    Cad = result.Cad,
                    CadId = cadId,
                    NativeFileId = result.Cad.NativeFileId,
                    IsReadOnly = true
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
    }
}
