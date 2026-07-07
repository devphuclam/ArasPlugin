using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Library
{
    public interface IArasOpenUrlService
    {
        string BuildPartUrl(string partId, string configId);

        string BuildCadUrl(string cadId);

        string BuildLibraryUrl(string libraryId);

        string BuildEntryUrl(string entryId);

        Task<ArasOpenUrlResult> BuildUrlAsync(ArasOpenUrlRequest request, CancellationToken cancellationToken);
    }
}
