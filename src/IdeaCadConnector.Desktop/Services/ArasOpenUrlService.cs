using System;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class ArasOpenUrlService : IArasOpenUrlService
    {
        private readonly Uri _baseUri;
        private readonly string _database;

        public ArasOpenUrlService(Uri baseUri, string database)
        {
            _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public string BuildPartUrl(string partId, string configId)
        {
            var id = !string.IsNullOrWhiteSpace(configId) ? configId : partId;
            return BuildItemUrl("Part", id);
        }

        public string BuildCadUrl(string cadId)
        {
            return BuildItemUrl("CAD", cadId);
        }

        public string BuildLibraryUrl(string libraryId)
        {
            return BuildItemUrl("idea_PartLibrary", libraryId);
        }

        public string BuildEntryUrl(string entryId)
        {
            return BuildItemUrl("idea_PartLibraryEntry", entryId);
        }

        public string BuildUserUrl(string userId)
        {
            return BuildItemUrl("User", userId);
        }

        private string BuildItemUrl(string itemType, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            // Aras Innovator client URL pattern:
            // {BaseUri}resource.aspx?id={itemId}&type={itemType}&db={database}
            var baseUri = _baseUri.ToString().TrimEnd('/');
            return $"{baseUri}/resource.aspx?id={Uri.EscapeDataString(itemId)}&type={Uri.EscapeDataString(itemType)}&db={Uri.EscapeDataString(_database)}";
        }
    }
}
