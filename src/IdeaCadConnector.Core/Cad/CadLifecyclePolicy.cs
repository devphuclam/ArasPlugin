using System;
using System.Collections.Generic;

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
                return "CAD is in review. Complete the NVTKC/TNTKC workflow in Aras before editing again.";

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
    }
}
