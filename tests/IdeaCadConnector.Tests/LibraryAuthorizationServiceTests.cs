using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Desktop;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class LibraryAuthorizationServiceTests
    {
        [Theory]
        [InlineData("lamEngineer", false, true, false, true)]
        [InlineData("lamPM", true, true, false, true)]
        [InlineData("admin", true, true, false, true)]
        [InlineData("InnovatorAdmin", true, true, false, true)]
        [InlineData("nvtkc", false, true, false, true)]
        [InlineData("tntkc", false, true, false, true)]
        [InlineData("tptkc", true, true, false, true)]
        [InlineData("unknown", false, false, true, false)]
        public void DefaultRoleMapping_IsConservativeAndMatchesUatUsers(
            string user,
            bool isManager,
            bool isContributorOrHigher,
            bool isReadOnlyViewer,
            bool canUsePartPicker)
        {
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = user, Connected = true });

            Assert.Equal(isManager, service.IsLibraryManager);
            Assert.Equal(isContributorOrHigher, service.IsContributorOrHigher);
            Assert.Equal(isReadOnlyViewer, service.IsReadOnlyViewer);
            Assert.Equal(isManager, service.CanManageLibraries);
            Assert.Equal(canUsePartPicker, service.CanUsePartPicker);
        }

        [Fact]
        public void DisconnectedUnknownUsers_DefaultToReadOnly()
        {
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = "unknown", Connected = false });

            Assert.False(service.IsLibraryManager);
            Assert.False(service.IsContributorOrHigher);
            Assert.False(service.CanManageLibraries);
            Assert.False(service.CanUsePartPicker);
            Assert.True(service.IsReadOnlyViewer);
        }

        [Fact]
        public void CustomRules_CanOverrideMappingForTests()
        {
            var rules = new LibraryAuthorizationRules(
                managerUsers: new[] { "qa-manager" },
                contributorUsers: new[] { "qa-contributor" });
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = "qa-contributor", Connected = true },
                rules);

            Assert.False(service.IsLibraryManager);
            Assert.True(service.IsContributorOrHigher);
            Assert.True(service.CanUsePartPicker);
        }

        private sealed class StubSessionContext : IAppSessionContext
        {
            public bool Connected { get; set; }
            public string CurrentUserName { get; set; }
            public IPdmRepositoryClient PdmClient { get; set; }
            public IArasCadClient ArasCadClient { get; set; }
            public IPartLibraryClient PartLibraryClient { get; set; }
            public PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }
            public string PendingLibraryFocusLibraryId { get; set; }
            public string PendingLibraryFocusEntryId { get; set; }
            public event System.EventHandler LibraryDataChanged;
            public event System.EventHandler LibraryWorkspaceRequested;
            public bool IsConnected => Connected;

            public void NotifyLibraryDataChanged()
            {
                LibraryDataChanged?.Invoke(this, System.EventArgs.Empty);
            }

            public void RequestLibraryWorkspace()
            {
                LibraryWorkspaceRequested?.Invoke(this, System.EventArgs.Empty);
            }
        }
    }
}
