using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Workspace.Models;

namespace IdeaCadConnector.Workspace.Recovery
{
    public interface IRecoveryCopyService
    {
        Task<RecoveryCopyResult> CreateRecoveryCopyAsync(
            string cadId, string workingFilePath, CancellationToken ct);
        string GetRecoveryDirectory(string cadId);
        Task CleanExpiredCopiesAsync(CancellationToken ct);
    }
}
