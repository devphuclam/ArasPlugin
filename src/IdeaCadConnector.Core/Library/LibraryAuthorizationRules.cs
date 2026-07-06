using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdeaCadConnector.Core.Library
{
    public sealed class LibraryAuthorizationRules
    {
        public static LibraryAuthorizationRules Default { get; } = new LibraryAuthorizationRules(
            managerUsers: new[] { "admin", "innovatoradmin", "lampm", "tptkc", "truongphongthietkeco" },
            contributorUsers: new[] { "lamengineer", "nvtkc", "tntkc" });

        public LibraryAuthorizationRules(
            IEnumerable<string> managerUsers = null,
            IEnumerable<string> contributorUsers = null)
        {
            ManagerUsers = new ReadOnlyCollection<string>(NormalizeAll(managerUsers));
            ContributorUsers = new ReadOnlyCollection<string>(NormalizeAll(contributorUsers));
        }

        public IReadOnlyCollection<string> ManagerUsers { get; }

        public IReadOnlyCollection<string> ContributorUsers { get; }

        public bool IsManager(string normalizedUser)
        {
            return !string.IsNullOrWhiteSpace(normalizedUser) && Contains(ManagerUsers, normalizedUser);
        }

        public bool IsContributorOrHigher(string normalizedUser)
        {
            return IsManager(normalizedUser) || (!string.IsNullOrWhiteSpace(normalizedUser) && Contains(ContributorUsers, normalizedUser));
        }

        private static bool Contains(IReadOnlyCollection<string> values, string value)
        {
            foreach (var item in values)
            {
                if (string.Equals(item, value, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> NormalizeAll(IEnumerable<string> values)
        {
            var list = new List<string>();
            if (values == null)
                return list;

            foreach (var value in values)
            {
                var normalized = Normalize(value);
                if (!string.IsNullOrWhiteSpace(normalized) &&
                    !list.Exists(item => string.Equals(item, normalized, System.StringComparison.OrdinalIgnoreCase)))
                    list.Add(normalized);
            }

            return list;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();
        }
    }
}
