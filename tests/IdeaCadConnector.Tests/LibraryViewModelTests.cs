using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class LibraryViewModelTests
    {
        [Fact]
        public async Task LibraryViewModel_UsesSessionClientAfterLogin_WhenConstructedBeforeLogin()
        {
            var session = new FakeAppSessionContext();
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-1", "Engineering Part Library", true)
                },
                SearchResponseToReturn = CreateSearchResponse(
                    "entry-1",
                    "lib-1",
                    "P-001",
                    "Released",
                    LibraryRevisionPolicy.LatestReleased)
            };

            var viewModel = new LibraryViewModel(session, null);

            session.PartLibraryClient = client;
            session.NotifyLibraryDataChanged();

            await WaitForAsync(() => viewModel.Libraries.Count == 1 && viewModel.Entries.Count == 1);

            Assert.Single(viewModel.Libraries);
            Assert.NotNull(viewModel.SelectedLibrary);
            Assert.Single(viewModel.Entries);
            Assert.Equal("lib-1", viewModel.SelectedLibrary.Id);
            Assert.Equal("Engineering Part Library", viewModel.SelectedLibrary.Name);
            Assert.Equal("entry-1", viewModel.Entries[0].EntryId);
            Assert.Equal(1, client.GetLibrariesCallCount);
            Assert.Equal(1, client.SearchEntriesCallCount);
            Assert.False(viewModel.LibrariesOverlayMessage.Contains("No accessible Libraries were returned.", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task LibraryViewModel_InjectedClientRemainsAuthoritative()
        {
            var session = new FakeAppSessionContext();
            var injectedClient = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-injected", "Injected Library", true)
                }
            };
            var sessionClient = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-session", "Session Library", true)
                }
            };

            var viewModel = new LibraryViewModel(session, injectedClient);

            session.PartLibraryClient = sessionClient;
            session.NotifyLibraryDataChanged();

            await WaitForAsync(() => viewModel.Libraries.Count == 1);

            Assert.Single(viewModel.Libraries);
            Assert.Equal("lib-injected", viewModel.Libraries[0].Id);
            Assert.Equal("Injected Library", viewModel.Libraries[0].Name);
            Assert.True(injectedClient.GetLibrariesCallCount >= 1);
            Assert.Equal(0, sessionClient.GetLibrariesCallCount);
        }

        [Fact]
        public async Task LibraryViewModel_PendingFocus_SelectsSavedLibraryAndEntry()
        {
            var session = new FakeAppSessionContext
            {
                PendingLibraryFocusLibraryId = "lib-1",
                PendingLibraryFocusEntryId = "entry-1"
            };

            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-1", "Engineering Part Library", true)
                },
                SearchResponseToReturn = CreateSearchResponse(
                    "entry-1",
                    "lib-1",
                    "P-001",
                    "Released",
                    LibraryRevisionPolicy.Pinned)
            };

            var viewModel = new LibraryViewModel(session, client);
            session.NotifyLibraryDataChanged();

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.SelectedEntry != null);

            Assert.Equal("lib-1", viewModel.SelectedLibrary.Id);
            Assert.Equal("entry-1", viewModel.SelectedEntry.EntryId);
            Assert.Null(session.PendingLibraryFocusLibraryId);
            Assert.Null(session.PendingLibraryFocusEntryId);
        }

        private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (predicate())
                    return;

                await Task.Delay(25);
            }

            Assert.True(predicate(), "Condition was not met before timeout.");
        }

        private static PartLibrarySummary CreateLibrary(string id, string name, bool canContribute)
        {
            return new PartLibrarySummary
            {
                Id = id,
                Name = name,
                CanContribute = canContribute,
                ItemCount = 1,
                LibraryType = LibraryType.Standard,
                IsPublic = true
            };
        }

        private static PartLibrarySearchResponse CreateSearchResponse(
            string entryId,
            string libraryId,
            string partNumber,
            string lifecycleState,
            LibraryRevisionPolicy revisionPolicy)
        {
            return new PartLibrarySearchResponse
            {
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 25,
                Entries = new[]
                {
                    new PartLibraryEntrySummary
                    {
                        EntryId = entryId,
                        LibraryId = libraryId,
                        LibraryName = "Engineering Part Library",
                        PartId = "part-1",
                        PartConfigId = "cfg-1",
                        PartNumber = partNumber,
                        PartName = "Test Part",
                        PartType = "Component",
                        Revision = "A",
                        LifecycleState = lifecycleState,
                        RevisionPolicy = revisionPolicy,
                        EntryStatus = LibraryEntryStatus.Published,
                        CadStatus = "Available"
                    }
                }
            };
        }

        private sealed class FakeAppSessionContext : IAppSessionContext
        {
            public FakeAppSessionContext()
            {
                PdmClient = new StubPdmRepositoryClient();
            }

            public IPdmRepositoryClient PdmClient { get; set; }
            public IArasCadClient ArasCadClient { get; set; }
            public IPartLibraryClient PartLibraryClient { get; set; }
            public string CurrentUserName { get; set; }
            public PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }
            public string PendingLibraryFocusLibraryId { get; set; }
            public string PendingLibraryFocusEntryId { get; set; }
            public event EventHandler LibraryDataChanged;
            public event EventHandler LibraryWorkspaceRequested;
            public bool IsConnected => PdmClient != null || ArasCadClient != null;

            public void NotifyLibraryDataChanged()
            {
                LibraryDataChanged?.Invoke(this, EventArgs.Empty);
            }

            public void RequestLibraryWorkspace()
            {
                LibraryWorkspaceRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class StubPdmRepositoryClient : IPdmRepositoryClient
        {
            public Task<PdmPushResult> PushAsync(PdmPushRequest request, CancellationToken ct)
                => Task.FromResult(new PdmPushResult());

            public Task<PdmExistencePreview> PreviewExistenceAsync(PdmPushRequest request, CancellationToken ct)
                => Task.FromResult(new PdmExistencePreview());

            public Task<PdmCloneResult> CloneLatestToWorkspaceAsync(PdmCloneRequest request, CancellationToken ct)
                => Task.FromResult(new PdmCloneResult());

            public Task<string> FindItemIdByNumberAsync(string itemType, string itemNumber, CancellationToken ct)
                => Task.FromResult<string>(null);

            public Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct)
                => Task.FromResult(new PdmReviseResult());

            public void Dispose()
            {
            }
        }

        private sealed class StubPartLibraryClient : IPartLibraryClient
        {
            public IReadOnlyList<PartLibrarySummary> LibrariesToReturn { get; set; } = Array.Empty<PartLibrarySummary>();
            public PartLibrarySearchResponse SearchResponseToReturn { get; set; } = new PartLibrarySearchResponse
            {
                Entries = Array.Empty<PartLibraryEntrySummary>(),
                TotalCount = 0,
                PageNumber = 1,
                PageSize = 25
            };
            public PartLibraryEntryDetails EntryDetailsToReturn { get; set; } = new PartLibraryEntryDetails();
            public ResolveLibraryPartResult ResolveResultToReturn { get; set; } = new ResolveLibraryPartResult();
            public IReadOnlyList<PartWhereUsedItem> WhereUsedToReturn { get; set; } = Array.Empty<PartWhereUsedItem>();
            public int GetLibrariesCallCount { get; private set; }
            public int SearchEntriesCallCount { get; private set; }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(CancellationToken cancellationToken)
            {
                GetLibrariesCallCount++;
                return Task.FromResult(LibrariesToReturn);
            }

            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
            {
                SearchEntriesCallCount++;
                return Task.FromResult(SearchResponseToReturn);
            }

            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(EntryDetailsToReturn);

            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new AddPartToLibraryResult { Success = true, EntryId = "entry-1" });

            public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult(ResolveResultToReturn);

            public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(WhereUsedToReturn);

            public Task RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public void Dispose()
            {
            }
        }
    }
}
