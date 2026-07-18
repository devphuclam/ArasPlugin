using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Core.Policies
{
    public sealed class MvpReleaseEligibility : ICadReleaseEligibility
    {
        private readonly ICadLifecyclePolicy _cadPolicy;
        private readonly IPartLifecyclePolicy _partPolicy;
        private readonly CadWorkflowGate _gate;

        public MvpReleaseEligibility(
            ICadLifecyclePolicy cadPolicy,
            IPartLifecyclePolicy partPolicy,
            CadWorkflowGate gate)
        {
            _cadPolicy = cadPolicy;
            _partPolicy = partPolicy;
            _gate = gate ?? throw new System.ArgumentNullException(nameof(gate));
        }

        public Task<CadReleaseEligibilityResult> CheckAsync(
            CadReleaseEligibilitySnapshot snapshot,
            CancellationToken ct)
        {
            var reasons = new List<string>();

            if (snapshot == null)
            {
                reasons.Add("Snapshot is null. Cannot evaluate release eligibility.");
                return Task.FromResult(new CadReleaseEligibilityResult
                {
                    IsEligible = false,
                    BlockingReasons = reasons.AsReadOnly()
                });
            }

            if (!_gate.IsPartReleaseAvailable())
            {
                reasons.Add(
                    "GATE-A Part release evidence is not recorded. The Part lifecycle policy " +
                    "cannot be used to authorize a release until verified Aras Part state names exist.");
                return Task.FromResult(new CadReleaseEligibilityResult
                {
                    IsEligible = false,
                    BlockingReasons = reasons.AsReadOnly()
                });
            }

            if (!_cadPolicy.CanApprove(snapshot.CadState))
            {
                reasons.Add($"CAD is in state '{snapshot.CadState}' which does not allow approval. CAD must be 'In Review'.");
            }

            if (!_partPolicy.CanRelease(snapshot.PartState))
            {
                reasons.Add($"Part is in state '{snapshot.PartState}' which does not allow release.");
            }

            return Task.FromResult(new CadReleaseEligibilityResult
            {
                IsEligible = reasons.Count == 0,
                BlockingReasons = reasons.AsReadOnly()
            });
        }
    }
}
