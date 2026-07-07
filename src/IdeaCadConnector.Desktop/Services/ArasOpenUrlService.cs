using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class ArasOpenUrlService : IArasOpenUrlService
    {
        private static readonly HashSet<string> ApprovedItemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Part",
            "CAD",
            "idea_PartLibrary",
            "idea_PartLibraryEntry"
        };

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

        public Task<ArasOpenUrlResult> BuildUrlAsync(ArasOpenUrlRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
                return Task.FromResult(new ArasOpenUrlResult
                {
                    Success = false,
                    ErrorMessage = "URL request is required.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                });

            if (string.IsNullOrWhiteSpace(request.ItemType))
                return Task.FromResult(new ArasOpenUrlResult
                {
                    Success = false,
                    ErrorMessage = "Item type is required.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                });

            if (string.IsNullOrWhiteSpace(request.ItemId) && string.IsNullOrWhiteSpace(request.ConfigId))
                return Task.FromResult(new ArasOpenUrlResult
                {
                    Success = false,
                    ErrorMessage = "Item ID or config ID is required.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                });

            if (!ApprovedItemTypes.Contains(request.ItemType))
                return Task.FromResult(new ArasOpenUrlResult
                {
                    Success = false,
                    ErrorMessage = $"Item type '{request.ItemType}' is not in the approved list for URL generation.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                });

            var id = !string.IsNullOrWhiteSpace(request.ConfigId) ? request.ConfigId : request.ItemId;
            var url = BuildItemUrl(request.ItemType, id);

            if (string.IsNullOrWhiteSpace(url))
                return Task.FromResult(new ArasOpenUrlResult
                {
                    Success = false,
                    ErrorMessage = "Failed to generate URL.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                });

            return Task.FromResult(new ArasOpenUrlResult
            {
                Success = true,
                Url = url
            });
        }

        private string BuildItemUrl(string itemType, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            var baseUri = _baseUri.ToString().TrimEnd('/');
            return $"{baseUri}/resource.aspx?id={Uri.EscapeDataString(itemId)}&type={Uri.EscapeDataString(itemType)}&db={Uri.EscapeDataString(_database)}";
        }
    }
}
