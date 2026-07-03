using System;
using System.Collections.Generic;

namespace IdeaCadConnector.Core.Library
{
    public static class PartLifecyclePolicy
    {
        public const string Obsolete = "Obsolete";

        private static readonly HashSet<string> NonReusableStates =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Obsolete
            };

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
    }
}
