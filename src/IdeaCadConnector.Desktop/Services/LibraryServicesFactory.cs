using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class LibraryServices
    {
        public LibraryServices(
            IPartLibraryVaultService vaultService,
            IIronCadOpenService ironCadOpenService,
            IArasOpenUrlService arasOpenUrlService,
            IBrowserLauncher browserLauncher)
        {
            VaultService = vaultService ?? throw new ArgumentNullException(nameof(vaultService));
            IronCadOpenService = ironCadOpenService ?? throw new ArgumentNullException(nameof(ironCadOpenService));
            ArasOpenUrlService = arasOpenUrlService ?? throw new ArgumentNullException(nameof(arasOpenUrlService));
            BrowserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        }

        public IPartLibraryVaultService VaultService { get; }

        public IIronCadOpenService IronCadOpenService { get; }

        public IArasOpenUrlService ArasOpenUrlService { get; }

        public IBrowserLauncher BrowserLauncher { get; }
    }

    internal static class LibraryServicesFactory
    {
        public static LibraryServices Create(IAppSessionContext session, IPartLibraryClient client = null)
        {
            session ??= AppSessionContext.Current;

            var vaultService = CreateVaultService(session, client);
            var ironCadService = CreateIronCadService(session);
            var openUrlService = CreateOpenUrlService(session);
            var browserLauncher = new BrowserLauncher();

            return new LibraryServices(vaultService, ironCadService, openUrlService, browserLauncher);
        }

        private static IPartLibraryVaultService CreateVaultService(IAppSessionContext session, IPartLibraryClient client)
        {
            if (session?.ArasCadClient == null || (client ?? session?.PartLibraryClient) == null)
                return new UnavailablePartLibraryVaultService();

            if (string.IsNullOrWhiteSpace(session.ArasServerUrl) || string.IsNullOrWhiteSpace(session.ArasDatabase))
                return new UnavailablePartLibraryVaultService();

            return new PartLibraryVaultService(
                session.ArasCadClient,
                partLibraryClient: client ?? session.PartLibraryClient,
                serverUrl: session.ArasServerUrl,
                database: session.ArasDatabase);
        }

        private static IIronCadOpenService CreateIronCadService(IAppSessionContext session)
        {
            var executablePath = session?.IronCadExecutablePath;
            var adapter = new IronCadExternalAdapter(executablePath);
            return new IronCadOpenService(adapter, executablePath);
        }

        private static IArasOpenUrlService CreateOpenUrlService(IAppSessionContext session)
        {
            if (session == null ||
                string.IsNullOrWhiteSpace(session.ArasServerUrl) ||
                string.IsNullOrWhiteSpace(session.ArasDatabase) ||
                !Uri.TryCreate(session.ArasServerUrl, UriKind.Absolute, out var baseUri))
            {
                return new UnavailableArasOpenUrlService();
            }

            return new ArasOpenUrlService(baseUri, session.ArasDatabase);
        }
    }

    internal sealed class UnavailablePartLibraryVaultService : IPartLibraryVaultService
    {
        public Task<PartLibraryCadFileInfo> GetPrimaryCadFileInfoAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<PartLibraryCadFileInfo>(CreateUnavailableException());

        public Task<VaultDownloadResult> DownloadToCacheAsync(PartLibraryCadFileInfo cadFileInfo, CancellationToken cancellationToken)
            => Task.FromResult(new VaultDownloadResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryStatusVaultServiceUnavailable),
                ErrorCode = ArasErrorCode.ValidationFailed
            });

        public string GetCachedFilePath(PartLibraryCadFileInfo cadFileInfo) => null;

        public string GetCachedFilePath(VaultCacheKey cacheKey) => null;

        public VaultCacheKey BuildCacheKey(string fileId, string revisionGeneration, string fileName, string userName)
            => new VaultCacheKey
            {
                FileId = fileId,
                RevisionGeneration = revisionGeneration ?? "0",
                FileName = fileName,
                UserName = userName
            };

        public void CleanTempOnFailure(string tempPath)
        {
        }

        private static ArasOperationException CreateUnavailableException()
        {
            return new ArasOperationException(
                ArasErrorCode.ValidationFailed,
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryStatusVaultServiceUnavailable));
        }
    }

    internal sealed class UnavailableArasOpenUrlService : IArasOpenUrlService
    {
        public string BuildPartUrl(string partId, string configId) => null;

        public string BuildCadUrl(string cadId) => null;

        public string BuildLibraryUrl(string libraryId) => null;

        public string BuildEntryUrl(string entryId) => null;

        public Task<ArasOpenUrlResult> BuildUrlAsync(ArasOpenUrlRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ArasOpenUrlResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryStatusOpenInArasRequiresUrl),
                ErrorCode = ArasErrorCode.ValidationFailed
            });
        }
    }
}
