using System;
using System.Collections.Generic;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Cad
{
    /// <summary>
    /// Client-side guardrails that mirror the live Custom CAD Document lifecycle.
    /// Aras workflow remains the authority for state transitions.
    /// </summary>
    public static class CadLifecyclePolicy
    {
        public const string Initial = "Khoi tao";
        public const string DetailedDesign = "Thiet ke chi tiet";
        public const string InReview = "In Review";
        public const string Released = "Released";
        public const string InChange = "In Change";
        public const string Superseded = "Superseded";
        public const string Obsolete = "Loai bo";

        private static readonly HashSet<string> EditableStates =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Initial,
                DetailedDesign
            };

        public static bool CanCheckout(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && EditableStates.Contains(state.Trim());
        }

        public static bool IsState(string state, string expected)
        {
            return string.Equals(
                state?.Trim(),
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCheckoutBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Refresh from Aras before checkout.";

            if (IsState(state, InReview))
                return "CAD is in review. Complete the ExampleContributor/ExampleReviewer workflow in Aras before editing again.";

            if (IsState(state, Released))
                return "CAD is released. Start the approved Aras change process before editing.";

            if (IsState(state, InChange))
                return "CAD is in change staging. Complete the approved Aras transition to 'Thiet ke chi tiet' before checkout.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "CAD is no longer active and cannot be checked out.";

            return $"CAD state '{state}' is not an editable state in the live Aras lifecycle.";
        }

        public static bool CanSubmitForReview(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && IsState(state, DetailedDesign);
        }

        public static bool CanStartDetailedDesign(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && IsState(state, Initial);
        }

        public static bool CanApproveReview(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && IsState(state, InReview);
        }

        public static bool CanRequestRework(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && IsState(state, InReview);
        }

        public static bool ShouldShowBusinessAction(CadBusinessActionKind kind, string state)
        {
            return CanExecuteBusinessAction(kind, state);
        }

        public static bool CanExecuteBusinessAction(CadBusinessActionKind kind, string state)
        {
            switch (kind)
            {
                case CadBusinessActionKind.StartDetailedDesign:
                    return CanStartDetailedDesign(state);
                case CadBusinessActionKind.SubmitForReview:
                    return CanSubmitForReview(state);
                case CadBusinessActionKind.Approve:
                    return CanApproveReview(state);
                case CadBusinessActionKind.RequestRework:
                    return CanRequestRework(state);
                default:
                    return false;
            }
        }

        public static string GetSubmitForReviewBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Cannot submit for review.";

            if (IsState(state, Initial))
                return "CAD must move to 'Thiet ke chi tiet' before submit for review becomes available.";

            if (IsState(state, InReview))
                return "CAD is already in review.";

            if (IsState(state, Released))
                return "CAD is already released.";

            if (IsState(state, InChange))
                return "CAD is in change staging. Complete the change process before submitting for review.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "CAD is no longer active and cannot be submitted for review.";

            return $"CAD state '{state}' does not allow submitting for review.";
        }

        // TODO(PERF-REVISION-SEAM): Move into IRevisionService when
        // server-side revise is implemented. Currently checks Released
        // state only; future must also check part existence and lock state.
        public static bool CanStartNewRevision(string state)
        {
            return !string.IsNullOrWhiteSpace(state) && IsState(state, Released);
        }

        // TODO(PERF-REVISION-SEAM): Extract into IRevisionService when
        // server-side revise is implemented. Currently guidance-only.
        public static string GetStartNewRevisionMessage(string cadNumber, string lifecycleState)
        {
            return
                $"The CAD \"{cadNumber}\" is in state \"{lifecycleState}\".\n\n" +
                "This desktop app does not create new revisions. To revise:\n" +
                "  1. Open the Aras web UI and create an ECO/change order for the linked Part.\n" +
                "  2. Promote the Part through \"In Change\" back to an editable state.\n" +
                "  3. Return here to check out the new working revision.";
        }

        public static string GetStartNewRevisionBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Cannot determine revision path.";

            if (CanStartNewRevision(state))
                return null;

            return $"CAD state '{state}' does not require a new revision path. Normal checkout is available.";
        }

        public static string GetStartDetailedDesignBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Cannot start detailed design.";

            if (IsState(state, DetailedDesign))
                return "CAD is already in 'Thiet ke chi tiet'.";

            if (IsState(state, InReview) || IsState(state, Released) || IsState(state, InChange))
                return $"CAD state '{state}' is already beyond the initial design step.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "CAD is no longer active and cannot enter detailed design.";

            return $"CAD state '{state}' does not allow starting detailed design.";
        }

        public static string GetApproveReviewBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Cannot approve review.";

            if (IsState(state, DetailedDesign))
                return "CAD must be submitted to 'In Review' before approve becomes available.";

            if (IsState(state, Released))
                return "CAD is already released.";

            if (IsState(state, Initial))
                return "CAD is still in 'Khoi tao'.";

            if (IsState(state, InChange) || IsState(state, Superseded) || IsState(state, Obsolete))
                return $"CAD state '{state}' does not allow direct approval.";

            return $"CAD state '{state}' does not allow approval.";
        }

        public static string GetRequestReworkBlockedMessage(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Cannot request rework.";

            if (IsState(state, DetailedDesign))
                return "CAD is already in 'Thiet ke chi tiet'.";

            if (IsState(state, Initial))
                return "CAD has not entered review yet.";

            if (IsState(state, Released))
                return "Released CAD cannot be sent back by this action.";

            if (IsState(state, InChange) || IsState(state, Superseded) || IsState(state, Obsolete))
                return $"CAD state '{state}' does not allow request rework.";

            return $"CAD state '{state}' does not allow request rework.";
        }

        public static string GetStateCategory(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "Unknown";
            if (CanCheckout(state))
                return "Editable";
            if (IsState(state, InReview))
                return "ReviewOnly";
            if (IsState(state, Released) || IsState(state, InChange))
                return "ReadOnly";
            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "Inactive";
            return "Unknown";
        }

        public static string GetStateSummary(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "CAD state is missing. Refresh from Aras.";

            if (IsState(state, Initial))
                return "Initial drafting stage (Khoi tao). Editable.";

            if (IsState(state, DetailedDesign))
                return "Detailed design stage (Thiet ke chi tiet). Editable.";

            if (IsState(state, InReview))
                return "Under review. Read-only. Reviewer actions available if you are assigned.";

            if (IsState(state, Released))
                return "Released and read-only. Use the approved change process for further work.";

            if (IsState(state, InChange))
                return "In a controlled change. Read-only until the change completes.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "Inactive. No longer in active use.";

            return $"State '{state}' is not recognized.";
        }

        public static string GetStateActionGuidance(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "Refresh Aras state before deciding what to do next.";

            if (IsState(state, Initial))
                return "Use Start Detailed Design to move this CAD into 'Thiet ke chi tiet', or Checkout to create the first local file.";

            if (IsState(state, DetailedDesign))
                return "Use Checkout to edit, or Submit for Review when design is complete.";

            if (IsState(state, InReview))
                return "Review in progress. Approve or Request Rework if you are the assigned reviewer. Otherwise wait for review to complete.";

            if (IsState(state, Released))
                return "Released CAD is read-only. A new revision requires a change order in the Aras web UI.";

            if (IsState(state, InChange))
                return "CAD is in a controlled change. Complete the approved change process in Aras before editing.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "This CAD is no longer active. Continue work through a replacement or new approved revision path.";

            return "Check the Aras web UI for details on this state.";
        }

        public static string GetUnlockedCadActionGuidance(string state, bool hasNativeFile)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "Refresh Aras state before deciding what to do next.";

            if (!hasNativeFile && CanCheckout(state))
                return "No native file exists yet. Checkout will create the first local IronCAD file.";

            if (!hasNativeFile)
                return "No native file exists, and the current Aras state does not allow checkout.";

            if (CanCheckout(state))
                return "CAD is in an editable state. Use Checkout to edit or Open Read-Only to inspect.";

            return GetStateActionGuidance(state);
        }

        public static string GetStaleSessionMessage(string liveState)
        {
            if (string.IsNullOrWhiteSpace(liveState))
                return "Local session is stale. Refresh Aras state before continuing.";

            return $"Local session is stale because live CAD state changed to '{liveState}'. Cancel checkout and refresh before editing.";
        }

        public static string GetStaleSessionLabel()
        {
            return "Local session stale";
        }

        public static string GetDifferentUserSessionMessage(string lockedBy)
        {
            if (string.IsNullOrWhiteSpace(lockedBy))
                return "Session belongs to a different user.";

            return $"Session belongs to {lockedBy}.";
        }

        public static string GetReadOnlySessionStaleMessage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Opened in read-only mode (session stale).";

            return $"Opened {fileName} in read-only mode (session stale).";
        }

        public static string GetCheckedOutByMeFileMissingStaleLabel()
        {
            return "Checked out by me (file missing, session stale)";
        }

        public static string GetPushSessionStaleMessage(string liveState)
        {
            if (string.IsNullOrWhiteSpace(liveState))
                return "Local checkout session is no longer valid. Cancel checkout before push.";

            return $"Live CAD state changed to '{liveState}'. Local checkout session is no longer valid. Cancel checkout before push.";
        }

        public static string GetRevisionDriftMessage(string lastKnownRevision, string liveRevision)
        {
            return $"CAD revision changed from {lastKnownRevision} to {liveRevision} since checkout. Push may create a conflict.";
        }

        public static string GetGenerationDriftMessage(long lastKnownGeneration, long liveGeneration)
        {
            return $"CAD generation changed from {lastKnownGeneration} to {liveGeneration} since checkout. Push may create a conflict.";
        }

        public static string GetWorkflowIdleStatusText(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "No active workflow task.";

            if (IsState(state, Initial))
                return "Initial CAD is ready to move into detailed design.";

            if (IsState(state, DetailedDesign))
                return "Design is ready to submit for review.";

            if (IsState(state, InReview))
                return "CAD is in review. Approve or Request Rework is available only to the assigned reviewer.";

            if (IsState(state, Released))
                return "CAD is released. Workflow actions are complete; use revision guidance for the next controlled change.";

            if (IsState(state, InChange))
                return "CAD is in change staging. Continue the approved Aras change process.";

            if (IsState(state, Superseded) || IsState(state, Obsolete))
                return "CAD is inactive. No workflow actions are available.";

            return "No active workflow task.";
        }
    }
}
