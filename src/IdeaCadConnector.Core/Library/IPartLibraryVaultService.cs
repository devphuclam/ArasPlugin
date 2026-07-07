using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Library
{
    public interface IPartLibraryVaultService
    {
        Task<PartLibraryCadFileInfo> GetPrimaryCadFileInfoAsync(string entryId, CancellationToken cancellationToken);

        Task<VaultDownloadResult> DownloadToCacheAsync(
            PartLibraryCadFileInfo cadFileInfo,
            CancellationToken cancellationToken);

        string GetCachedFilePath(PartLibraryCadFileInfo cadFileInfo);

        string GetCachedFilePath(VaultCacheKey cacheKey);

        VaultCacheKey BuildCacheKey(string fileId, string revisionGeneration, string fileName, string userName);

        void CleanTempOnFailure(string tempPath);
    }
}
