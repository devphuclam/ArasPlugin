using System.Collections.Generic;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Cad
{
    /// <summary>
    /// Centralizes the Aras evidence gates for controlled CAD workflow actions.
    /// Some actions are safe to expose as soon as the live Aras lifecycle allows
    /// them (StartDetailedDesign, SubmitForReview). Others require verified Aras
    /// evidence before the client may drive the transition, so they stay disabled
    /// until their gate is explicitly opened.
    /// </summary>
    public sealed class CadWorkflowGate
    {
        private static readonly HashSet<CadBusinessActionKind> GatedByEvidence =
            new HashSet<CadBusinessActionKind>
            {
                CadBusinessActionKind.Approve,
                CadBusinessActionKind.RequestRework,
                CadBusinessActionKind.Withdraw
            };

        private readonly HashSet<CadBusinessActionKind> _openGates;
        private bool _partReleaseGateOpen;

        public CadWorkflowGate()
        {
            _openGates = new HashSet<CadBusinessActionKind>();
        }

        /// <summary>True when the action is held closed until Aras evidence is recorded.</summary>
        public static bool IsGated(CadBusinessActionKind kind)
        {
            return GatedByEvidence.Contains(kind);
        }

        /// <summary>
        /// Opens the GATE-A Part release gate. Until this is called, the Part
        /// lifecycle policy must not drive runtime approval/release decisions
        /// because the verified Aras Part state names are still pending.
        /// </summary>
        public void OpenPartReleaseGate()
        {
            lock (_openGates)
            {
                _partReleaseGateOpen = true;
            }
        }

        /// <summary>Closes the GATE-A Part release gate (e.g. evidence retracted).</summary>
        public void ClosePartReleaseGate()
        {
            lock (_openGates)
            {
                _partReleaseGateOpen = false;
            }
        }

        /// <summary>
        /// False until the GATE-A Part evidence is recorded. When false, the
        /// Part lifecycle policy cannot be used to authorize a release.
        /// </summary>
        public bool IsPartReleaseAvailable()
        {
            lock (_openGates)
            {
                return _partReleaseGateOpen;
            }
        }

        /// <summary>Records that the Aras evidence for the given action is verified.</summary>
        public void OpenGate(CadBusinessActionKind kind)
        {
            lock (_openGates)
            {
                _openGates.Add(kind);
            }
        }

        /// <summary>Closes a previously opened gate (e.g. evidence retracted).</summary>
        public void CloseGate(CadBusinessActionKind kind)
        {
            lock (_openGates)
            {
                _openGates.Remove(kind);
            }
        }

        /// <summary>
        /// Returns true when the action may be shown/executed. Actions that are not
        /// evidence-gated are available by default. Evidence-gated actions remain
        /// disabled until their gate is opened via <see cref="OpenGate"/>.
        /// </summary>
        public bool IsAvailable(CadBusinessActionKind kind)
        {
            if (!IsGated(kind))
                return true;

            lock (_openGates)
            {
                return _openGates.Contains(kind);
            }
        }
    }
}
