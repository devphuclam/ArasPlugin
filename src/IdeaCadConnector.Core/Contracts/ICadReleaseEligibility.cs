using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Contracts
{
    public interface ICadReleaseEligibility
    {
        Task<CadReleaseEligibilityResult> CheckAsync(
            CadReleaseEligibilitySnapshot snapshot,
            CancellationToken ct);
    }
}
