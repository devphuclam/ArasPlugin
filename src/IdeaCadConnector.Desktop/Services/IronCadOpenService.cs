using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
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

        public async Task OpenCadFileAsync(string filePath, CadOpenMode openMode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArasOperationException(
                    ArasErrorCode.ValidationFailed,
                    "File path is required to open in IronCAD.");

            if (!File.Exists(filePath))
                throw new ArasOperationException(
                    ArasErrorCode.CadNotFound,
                    $"CAD file not found at: {filePath}");

            if (!IsIronCadAvailable)
                throw new ArasOperationException(
                    ArasErrorCode.FileUploadNotFound,
                    "IronCAD executable is not available. Check the configured path.");

            cancellationToken.ThrowIfCancellationRequested();

            await _adapter.OpenDocumentAsync(filePath, openMode, cancellationToken).ConfigureAwait(false);
        }
    }
}
