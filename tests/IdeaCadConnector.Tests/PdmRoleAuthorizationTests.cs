using System.Collections.Generic;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Configuration;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Policies;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class TestPdmRoleProvider : IPdmRoleProvider
    {
        public PdmUserRole GetRole(string userName)
        {
            if (string.Equals(userName, "pm-a", System.StringComparison.OrdinalIgnoreCase))
                return PdmUserRole.ProjectManager;
            if (string.Equals(userName, "admin-a", System.StringComparison.OrdinalIgnoreCase))
                return PdmUserRole.PdmAdministrator;
            if (string.Equals(userName, "engineer-a", System.StringComparison.OrdinalIgnoreCase))
                return PdmUserRole.DesignEngineer;
            if (string.IsNullOrWhiteSpace(userName))
                return PdmUserRole.DesignEngineer;
            return PdmUserRole.Reviewer;
        }
    }

    public sealed class PdmRoleAuthorizationTests
    {
        [Fact]
        public void ConfiguredProvider_ResolvesConfiguredRolesWithoutGuessingIdentity()
        {
            var provider = new ConfiguredPdmRoleProvider(new RoleConfiguration
            {
                ContributorUsers = new List<string> { "engineer-a" },
                ReviewerUsers = new List<string> { "reviewer-a" },
                ManagerUsers = new List<string> { "pm-a" },
                PdmAdministratorUsers = new List<string> { "pdm-admin-a" },
                ReadOnlyUsers = new List<string> { "viewer-a" }
            });

            Assert.Equal(PdmUserRole.DesignEngineer, provider.GetRole("ENGINEER-A"));
            Assert.Equal(PdmUserRole.Reviewer, provider.GetRole("reviewer-a"));
            Assert.Equal(PdmUserRole.ProjectManager, provider.GetRole("pm-a"));
            Assert.Equal(PdmUserRole.PdmAdministrator, provider.GetRole("pdm-admin-a"));
            Assert.Equal(PdmUserRole.ProjectManager, provider.GetRole("viewer-a"));
            Assert.Equal(PdmUserRole.Unknown, provider.GetRole("unconfigured-user"));
        }

        [Fact]
        public void RolePolicy_AllowsRoleOwnedActionsAndAdministratorOverride()
        {
            Assert.True(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.DesignEngineer, CadBusinessActionKind.StartDetailedDesign));
            Assert.True(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.DesignEngineer, CadBusinessActionKind.SubmitForReview));
            Assert.True(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.Reviewer, CadBusinessActionKind.Approve));
            Assert.True(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.Reviewer, CadBusinessActionKind.RequestRework));
            Assert.False(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.ProjectManager, CadBusinessActionKind.StartDetailedDesign));
            Assert.True(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.PdmAdministrator, CadBusinessActionKind.Approve));
            Assert.False(PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.Unknown, CadBusinessActionKind.SubmitForReview));
        }

        [Fact]
        public void RolePolicy_BlocksEngineeringWorkspaceOperationsForUnauthorizedRoles()
        {
            Assert.True(PdmRolePolicy.CanCheckout(PdmUserRole.DesignEngineer));
            Assert.True(PdmRolePolicy.CanCheckIn(PdmUserRole.DesignEngineer));
            Assert.True(PdmRolePolicy.CanCancelCheckout(PdmUserRole.DesignEngineer));
            Assert.True(PdmRolePolicy.CanStartNewRevision(PdmUserRole.DesignEngineer));
            Assert.False(PdmRolePolicy.CanCheckout(PdmUserRole.Reviewer));
            Assert.False(PdmRolePolicy.CanCheckIn(PdmUserRole.ProjectManager));
            Assert.True(PdmRolePolicy.CanStartNewRevision(PdmUserRole.PdmAdministrator));
        }

        [Fact]
        public void RolePolicy_AllowsPdmAdministratorAllCadBusinessActions()
        {
            foreach (CadBusinessActionKind action in System.Enum.GetValues(typeof(CadBusinessActionKind)))
            {
                Assert.True(
                    PdmRolePolicy.CanExecuteCadBusinessAction(PdmUserRole.PdmAdministrator, action),
                    $"PDM Administrator should be allowed through the client role policy for {action}.");
            }
        }

        [Fact]
        public void RolePolicy_AllowsPdmAdministratorReviewOverrideWithoutAssignment()
        {
            Assert.True(PdmRolePolicy.CanBypassReviewerAssignment(PdmUserRole.PdmAdministrator));
            Assert.False(PdmRolePolicy.CanBypassReviewerAssignment(PdmUserRole.Reviewer));
            Assert.False(PdmRolePolicy.CanBypassReviewerAssignment(PdmUserRole.DesignEngineer));
        }

        [Fact]
        public void RolePolicy_AllowsPdmAdministratorAllWorkspaceOperations()
        {
            Assert.True(PdmRolePolicy.CanCheckout(PdmUserRole.PdmAdministrator));
            Assert.True(PdmRolePolicy.CanCheckIn(PdmUserRole.PdmAdministrator));
            Assert.True(PdmRolePolicy.CanCancelCheckout(PdmUserRole.PdmAdministrator));
            Assert.True(PdmRolePolicy.CanStartNewRevision(PdmUserRole.PdmAdministrator));
        }

        [Fact]
        public void ConfiguredProvider_FailsClosedWhenUserMatchesMultipleRoleLists()
        {
            var provider = new ConfiguredPdmRoleProvider(new RoleConfiguration
            {
                ManagerUsers = new List<string> { "ambiguous" },
                ReadOnlyUsers = new List<string> { "ambiguous" }
            });

            Assert.Equal(PdmUserRole.Unknown, provider.GetRole("ambiguous"));
        }
    }
}
