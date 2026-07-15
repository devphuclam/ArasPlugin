using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Aras
{
    internal interface IVaultFileClient
    {
        Task<string> UploadFileAsync(
            string filePath,
            string fileName,
            CancellationToken ct);

        Task<string> DownloadFileAsync(
            string fileId,
            string targetDirectory,
            CancellationToken ct);
    }
}
