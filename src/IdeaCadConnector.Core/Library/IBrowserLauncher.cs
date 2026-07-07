using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Library
{
    public interface IBrowserLauncher
    {
        Task<bool> LaunchUrlAsync(string url, CancellationToken cancellationToken);
    }
}
