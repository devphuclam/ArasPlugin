using System;
using System.Collections.Generic;

namespace IdeaCadConnector.Core.Library
{
    /// <summary>
    /// Backend-neutral Part lifecycle policy for Feature 003's bounded MVP
    /// profile. Part state names are intentionally owned here rather than
    /// reused from the CAD policy.
    ///
    /// The MVP lifecycle ends at Released. States after Released are not
    /// interpreted by this policy.
    /// </summary>
    public sealed class PartLifecyclePolicy : IPartLifecyclePolicy
    {
        public const string KhoiTao = "Khoi tao";
        public const string ThietKeChiTiet = "Thiet ke chi tiet";
        public const string InReview = "In Review";
        public const string Released = "Released";
        public const string Obsolete = "Obsolete";

        private static readonly HashSet<string> NonReusableStates =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Obsolete
            };

        /// <summary>
        /// Only the review state is eligible for the coordinated MVP release.
        /// The authority remains the source of truth for the actual transition.
        /// </summary>
        public bool CanRelease(string state)
        {
            return IsState(state, InReview);
        }

        public bool IsReleased(string state)
        {
            return IsState(state, Released);
        }

        // Backward-compatible helpers used by the Part Library feature.
        public static bool IsPartObsolete(string state)
        {
            return !string.IsNullOrWhiteSpace(state)
                && NonReusableStates.Contains(state.Trim());
        }

        public static bool IsReusable(string state)
        {
            return string.IsNullOrWhiteSpace(state)
                || !NonReusableStates.Contains(state.Trim());
        }

        public static string GetPartNotReusableMessage(string state, string partNumber)
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            if (IsPartObsolete(state))
                return $"Part reuse failed: Part is in state '{state}' and cannot be reused.";

            return null;
        }

        private static bool IsState(string state, string expected)
        {
            return !string.IsNullOrWhiteSpace(state)
                && string.Equals(state.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
