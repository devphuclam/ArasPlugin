using System;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;

namespace IdeaCadConnector.Desktop
{
    public sealed class GuidanceRevisionService : IRevisionService
    {
        public static string BuildReadinessText(
            PdmRevisePreconditionResult preconditions,
            string readyMessage,
            string unavailableMessage)
        {
            if (preconditions == null)
                return unavailableMessage ?? string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            if (preconditions.BlockingReasons is { Count: > 0 })
                parts.AddRange(preconditions.BlockingReasons);
            if (preconditions.Warnings is { Count: > 0 })
                parts.AddRange(preconditions.Warnings);

            if (parts.Count > 0)
                return string.Join(" | ", parts);

            return preconditions.CanRevise
                ? (readyMessage ?? string.Empty)
                : (unavailableMessage ?? string.Empty);
        }

        public static bool ShouldShowRevisionEntryPoint(string cadId, string readinessText)
        {
            return !string.IsNullOrWhiteSpace(cadId)
                && !string.IsNullOrWhiteSpace(readinessText);
        }

        public Task<PdmRevisePreconditionResult> CheckPreconditionsAsync(
            string cadState,
            string cadId,
            string partId,
            string lockToken,
            CancellationToken ct)
        {
            var blocking = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();

            var isReleased = !string.IsNullOrWhiteSpace(cadState)
                && CadLifecyclePolicy.IsState(cadState, CadLifecyclePolicy.Released);
            if (!isReleased)
                blocking.Add("CAD is not in Released state.");

            if (string.IsNullOrWhiteSpace(cadId))
                blocking.Add("CAD Aras ID is not known.");

            if (string.IsNullOrWhiteSpace(partId))
                blocking.Add("Part Aras ID is not tracked. Cannot verify linked Part for revision.");

            if (!string.IsNullOrWhiteSpace(lockToken))
                blocking.Add("Active local checkout detected. Cancel checkout before revision.");

            var result = new PdmRevisePreconditionResult
            {
                CanRevise = blocking.Count == 0,
                BlockingReasons = blocking,
                Warnings = warnings
            };

            return Task.FromResult(result);
        }

        public async Task<PdmReviseResult> ReviseAsync(PdmReviseRequest request, CancellationToken ct)
        {
            var repoClient = MainViewModel.SharedPdmClient;
            if (repoClient == null)
            {
                return new PdmReviseResult
                {
                    Success = false,
                    ErrorMessage = "Not connected to Aras. Sign in and select a CAD to create a new revision."
                };
            }

            return await repoClient.ReviseCadAsync(request, ct);
        }
    }
}
