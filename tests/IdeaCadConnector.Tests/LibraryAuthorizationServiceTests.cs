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
        [InlineData("lamEngineer", false, true, false, true, false, false)]
        [InlineData("lamPM", true, true, true, true, true, true)]
        [InlineData("admin", true, true, true, true, true, true)]
        [InlineData("InnovatorAdmin", true, true, true, true, true, true)]
        [InlineData("nvtkc", false, true, false, true, false, false)]
        [InlineData("tntkc", false, true, true, true, true, true)]
        [InlineData("tptkc", true, true, true, true, true, true)]
        [InlineData("unknown", false, false, false, false, false, false)]
        public void DefaultRoleMapping_IsConservativeAndMatchesUatUsers(
            string user,
            bool isManager,
            bool isContributorOrHigher,
            bool isReviewerOrHigher,
            bool canUsePartPicker,
            bool canMoveEntries,
            bool canPinRevisions)
        {
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = user, Connected = true });

            Assert.Equal(isManager, service.IsLibraryManager);
            Assert.Equal(isContributorOrHigher, service.IsContributorOrHigher);
            Assert.Equal(isReviewerOrHigher, service.IsReviewerOrHigher);
            Assert.Equal(isManager, service.CanManageLibraries);
            Assert.Equal(canUsePartPicker, service.CanUsePartPicker);
            Assert.Equal(canMoveEntries, service.CanMoveEntries);
            Assert.Equal(canPinRevisions, service.CanPinRevisions);
        }

        [Fact]
        public void DisconnectedUnknownUsers_DefaultToReadOnly()
        {
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = "unknown", Connected = false });

            Assert.False(service.IsLibraryManager);
            Assert.False(service.IsContributorOrHigher);
            Assert.False(service.IsReviewerOrHigher);
            Assert.False(service.CanManageLibraries);
            Assert.False(service.CanUsePartPicker);
            Assert.False(service.CanMoveEntries);
            Assert.False(service.CanPinRevisions);
            Assert.True(service.IsReadOnlyViewer);
        }

        [Fact]
        public void CustomRules_CanOverrideMappingForTests()
        {
            var rules = new LibraryAuthorizationRules(
                managerUsers: new[] { "qa-manager" },
                reviewerUsers: new[] { "qa-reviewer" },
                contributorUsers: new[] { "qa-contributor" });
            var service = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = "qa-contributor", Connected = true },
                rules);

            Assert.False(service.IsLibraryManager);
            Assert.True(service.IsContributorOrHigher);
            Assert.False(service.IsReviewerOrHigher);
            Assert.True(service.CanUsePartPicker);
            Assert.False(service.CanMoveEntries);
            Assert.False(service.CanPinRevisions);

            var reviewerService = new LibraryAuthorizationService(
                new StubSessionContext { CurrentUserName = "qa-reviewer", Connected = true },
                rules);

            Assert.False(reviewerService.IsLibraryManager);
            Assert.False(reviewerService.IsContributorOrHigher);
            Assert.True(reviewerService.IsReviewerOrHigher);
            Assert.False(reviewerService.CanUsePartPicker);
            Assert.True(reviewerService.CanMoveEntries);
            Assert.True(reviewerService.CanPinRevisions);
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
