using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Contracts
{
    public interface IRevisionService
    {
        Task<PdmRevisePreconditionResult> CheckPreconditionsAsync(
            string cadState,
            string cadId,
            string partId,
            string lockToken,
            CancellationToken ct);

        Task<PdmReviseResult> ReviseAsync(
            PdmReviseRequest request,
            CancellationToken ct);
    }
}
