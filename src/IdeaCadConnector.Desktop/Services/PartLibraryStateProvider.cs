using System;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Desktop.Services
{
    /// <summary>
    /// Adapter at the Desktop seam that obtains the current Part lifecycle state
    /// from the already-authenticated Part library client. The provider returns
    /// null when the client or authoritative state is unavailable so callers
    /// remain fail-closed.
    /// </summary>
    public sealed class PartLibraryStateProvider : IPartStateProvider
    {
        private readonly Func<IPartLibraryClient> _clientAccessor;

        public PartLibraryStateProvider(Func<IPartLibraryClient> clientAccessor)
        {
            _clientAccessor = clientAccessor
                ?? throw new ArgumentNullException(nameof(clientAccessor));
        }

        public async Task<string> GetPartStateAsync(string partId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(partId))
                return null;

            var client = _clientAccessor();
            if (client == null)
                return null;

            var preview = await client.GetPartPreviewAsync(partId, ct).ConfigureAwait(false);
            return preview?.LifecycleState;
        }
    }
}
