using System.Threading;
using System.Threading.Tasks;

namespace IdeaCadConnector.Core.Cad
{
    /// <summary>
    /// Backend-neutral seam for retrieving the authoritative lifecycle state
    /// of a Part revision. Implementations must not fabricate or default
    /// state names. When no authoritative source is available, return null
    /// so callers can block actions that depend on Part state.
    /// </summary>
    public interface IPartStateProvider
    {
        /// <summary>
        /// Returns the authoritative lifecycle state for the Part revision
        /// identified by <paramref name="partId"/>, or null when the state
        /// cannot be determined.
        /// </summary>
        Task<string> GetPartStateAsync(string partId, CancellationToken ct);
    }
}
