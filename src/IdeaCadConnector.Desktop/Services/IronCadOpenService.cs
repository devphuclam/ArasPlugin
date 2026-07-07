using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class IronCadOpenService : IIronCadOpenService
    {
        private readonly ICadApplicationAdapter _adapter;
        private readonly string _ironCadExecutablePath;

        public IronCadOpenService(
            ICadApplicationAdapter adapter,
            string ironCadExecutablePath = @"C:\Program Files\IronCAD\2025\bin\IRONCAD.exe")
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _ironCadExecutablePath = ironCadExecutablePath;
        }

        public bool IsIronCadAvailable
        {
            get
            {
                try
                {
                    return !string.IsNullOrWhiteSpace(_ironCadExecutablePath)
                        && File.Exists(_ironCadExecutablePath);
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<IronCadOpenResult> OpenCadFileAsync(IronCadOpenRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "Open request is required.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };

            if (string.IsNullOrWhiteSpace(request.FilePath))
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "File path is required to open in IronCAD.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };

            // D-06: Reject remote URLs
            if (request.IsRemoteUrl)
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "Remote URLs are not supported. File must be downloaded locally first.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };

            // D-06: Reject zero-byte files
            if (request.FileSize == 0)
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "Cannot open a zero-byte file.",
                    ErrorCode = ArasErrorCode.FileUploadNotFound
                };

            // D-06: Validate file extension
            var ext = Path.GetExtension(request.FilePath);
            if (!VaultFileValidator.IsExtensionAllowed(ext))
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = $"File extension '{ext}' is not supported by IronCAD.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                };

            // D-06: Reject untrusted source
            if (!request.IsTrustedSource)
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "File is from an untrusted source and cannot be opened.",
                    ErrorCode = ArasErrorCode.PermissionDenied
                };

            if (!File.Exists(request.FilePath))
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = $"CAD file not found at: {request.FilePath}",
                    ErrorCode = ArasErrorCode.CadNotFound
                };

            if (!IsIronCadAvailable)
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = "IronCAD executable is not available. Check the configured path.",
                    ErrorCode = ArasErrorCode.FileUploadNotFound
                };

            cancellationToken.ThrowIfCancellationRequested();

            // Prefer adapter path before process fallback
            try
            {
                await _adapter.OpenDocumentAsync(request.FilePath, request.OpenMode, cancellationToken).ConfigureAwait(false);
                return new IronCadOpenResult { Success = true };
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new IronCadOpenResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to open file in IronCAD: {ex.Message}",
                    ErrorCode = ArasErrorCode.UnexpectedServerError
                };
            }
        }
    }
}
