using System;
using System.Collections.Generic;
using System.Linq;
using IdeaCadConnector.Core.Contracts;

namespace IdeaCadConnector.Core.Configuration
{
    public sealed class ConfiguredPdmRoleProvider : IPdmRoleProvider
    {
        private readonly RoleConfiguration _configuration;

        public ConfiguredPdmRoleProvider(RoleConfiguration configuration)
        {
            _configuration = configuration ?? new RoleConfiguration();
        }

        public PdmUserRole GetRole(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return PdmUserRole.Unknown;

            var normalizedUserName = userName.Trim();
            var matches = new List<PdmUserRole>();

            if (Contains(_configuration.PdmAdministratorUsers, normalizedUserName))
                matches.Add(PdmUserRole.PdmAdministrator);
            if (Contains(_configuration.ManagerUsers, normalizedUserName))
                matches.Add(PdmUserRole.ProjectManager);
            if (Contains(_configuration.ReviewerUsers, normalizedUserName))
                matches.Add(PdmUserRole.Reviewer);
            if (Contains(_configuration.ContributorUsers, normalizedUserName))
                matches.Add(PdmUserRole.DesignEngineer);
            if (Contains(_configuration.ReadOnlyUsers, normalizedUserName))
                matches.Add(PdmUserRole.ProjectManager);

            return matches.Count == 1 ? matches[0] : PdmUserRole.Unknown;
        }

        private static bool Contains(IEnumerable<string> users, string userName)
        {
            return users?.Any(user => string.Equals(user?.Trim(), userName, StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
