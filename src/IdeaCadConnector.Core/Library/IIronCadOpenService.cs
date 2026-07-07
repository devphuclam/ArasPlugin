using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;

namespace IdeaCadConnector.Core.Library
{
    public interface IIronCadOpenService
    {
        bool IsIronCadAvailable { get; }

        Task OpenCadFileAsync(string filePath, CadOpenMode openMode, CancellationToken cancellationToken);
    }
}
