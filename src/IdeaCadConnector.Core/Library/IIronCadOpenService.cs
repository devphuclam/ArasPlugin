using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Library
{
    public interface IIronCadOpenService
    {
        bool IsIronCadAvailable { get; }

        Task<IronCadOpenResult> OpenCadFileAsync(IronCadOpenRequest request, CancellationToken cancellationToken);
    }
}
