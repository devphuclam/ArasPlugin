using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Workspace;
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

        [Fact]
        public async Task LibraryViewModel_InvalidEntry_DoesNotResolveForAddToProject()
        {
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = new PdmProjectsViewModel()
            };
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-1", "Engineering Part Library", true)
                },
                SearchResponseToReturn = new PartLibrarySearchResponse
                {
                    TotalCount = 1,
                    PageNumber = 1,
                    PageSize = 25,
                    Entries = new[]
                    {
                        new PartLibraryEntrySummary
                        {
                            EntryId = "entry-1",
                            LibraryId = "lib-1",
                            LibraryName = "Engineering Part Library",
                            PartId = "part-1",
                            PartConfigId = "cfg-1",
                            PartNumber = "P-001",
                            PartName = "Broken Part",
                            Revision = "A",
                            RevisionPolicy = LibraryRevisionPolicy.LatestReleased,
                            LifecycleState = "Released",
                            EntryLifecycleState = "Draft",
                            EntryStatus = LibraryEntryStatus.Draft,
                            ResolutionFailed = true,
                            ResolutionError = "No released revision is available.",
                            CanAddToProject = false
                        }
                    }
                },
                EntryDetailsToReturn = new PartLibraryEntryDetails
                {
                    EntryId = "entry-1",
                    PartId = "part-1",
                    PartConfigId = "cfg-1",
                    PartNumber = "P-001",
                    PartName = "Broken Part",
                    Revision = "A",
                    LifecycleState = "Released",
                    EntryLifecycleState = "Draft",
                    EntryStatus = LibraryEntryStatus.Draft,
                    ResolutionFailed = true,
                    ResolutionError = "No released revision is available.",
                    CanAddToProject = false
                }
            };

            var viewModel = new LibraryViewModel(session, client);
            session.NotifyLibraryDataChanged();
            await WaitForAsync(() => viewModel.SelectedEntry != null);

            Assert.False(viewModel.AddToCurrentProjectCommand.CanExecute(null));
            viewModel.AddToCurrentProjectCommand.Execute(null);

            Assert.Equal(0, client.ResolveUsingStoredPolicyCallCount);
            Assert.Contains("No released revision", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LibraryViewModel_DeprecatedEntry_DisablesAddToCurrentProject()
        {
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                })
            };
            var client = CreateReusableClient();
            client.SearchResponseToReturn.Entries[0].EntryStatus = LibraryEntryStatus.Deprecated;
            client.SearchResponseToReturn.Entries[0].IsDeprecated = true;

            var viewModel = new LibraryViewModel(session, client);
            await WaitForAsync(() => viewModel.SelectedEntry != null);

            Assert.False(viewModel.AddToCurrentProjectCommand.CanExecute(null));
            viewModel.AddToCurrentProjectCommand.Execute(null);
            Assert.Equal(0, client.ResolveUsingStoredPolicyCallCount);
        }

        [Fact]
        public async Task AddToCurrentProject_DoesNotOpenDialogWithoutValidParentCandidates()
        {
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(Array.Empty<PdmStructureNode>())
            };
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", "Engineering Part Library", true) },
                SearchResponseToReturn = CreateSearchResponse("entry-1", "lib-1", "P-001", "Released", LibraryRevisionPolicy.LatestReleased),
                EntryDetailsToReturn = new PartLibraryEntryDetails
                {
                    EntryId = "entry-1",
                    LibraryId = "lib-1",
                    LibraryName = "Engineering Part Library",
                    PartId = "part-1",
                    PartConfigId = "cfg-1",
                    PartNumber = "P-001",
                    PartName = "Test Part",
                    Revision = "A",
                    RevisionPolicy = LibraryRevisionPolicy.LatestReleased,
                    PrimaryCadFileName = "P-001.ics",
                    CanAddToProject = true,
                    ResolutionFailed = false
                },
                ResolveResultToReturn = new ResolveLibraryPartResult
                {
                    ResolvedPartId = "part-1",
                    ResolvedPartConfigId = "cfg-1",
                    ResolvedRevision = "A"
                }
            };
            client.SearchResponseToReturn.Entries[0].CanAddToProject = true;
            client.SearchResponseToReturn.Entries[0].ResolutionFailed = false;

            var viewModel = new LibraryViewModel(session, client);
            session.NotifyLibraryDataChanged();
            await WaitForAsync(() => viewModel.SelectedEntry != null);

            var dialogOpened = false;
            viewModel.AddToCurrentProjectDialogHandler = _ =>
            {
                dialogOpened = true;
                return true;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            await WaitForAsync(() => viewModel.StatusMessage == "Current PDM Project does not contain a valid target parent Part. Analyze or open a valid PDM Project first.");

            Assert.False(dialogOpened);
            Assert.Equal(0, client.ResolveUsingStoredPolicyCallCount);
            Assert.Equal("Current PDM Project does not contain a valid target parent Part. Analyze or open a valid PDM Project first.", viewModel.StatusMessage);
        }

        [Fact]
        public async Task AddToCurrentProject_AcceptedDialogWithoutSelectedParent_IsRejectedSafely()
        {
            using var folder = new TempWorkspaceFolder();
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                }, folder.Path)
            };
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", "Engineering Part Library", true) },
                SearchResponseToReturn = CreateSearchResponse("entry-1", "lib-1", "P-001", "Released", LibraryRevisionPolicy.LatestReleased),
                EntryDetailsToReturn = new PartLibraryEntryDetails
                {
                    EntryId = "entry-1",
                    LibraryId = "lib-1",
                    LibraryName = "Engineering Part Library",
                    PartId = "part-1",
                    PartConfigId = "cfg-1",
                    PartNumber = "P-001",
                    PartName = "Test Part",
                    Revision = "A",
                    RevisionPolicy = LibraryRevisionPolicy.LatestReleased,
                    PrimaryCadFileName = "P-001.ics",
                    CanAddToProject = true,
                    ResolutionFailed = false
                },
                ResolveResultToReturn = new ResolveLibraryPartResult
                {
                    ResolvedPartId = "part-1",
                    ResolvedPartConfigId = "cfg-1",
                    ResolvedRevision = "A"
                }
            };
            client.SearchResponseToReturn.Entries[0].CanAddToProject = true;
            client.SearchResponseToReturn.Entries[0].ResolutionFailed = false;

            var viewModel = new LibraryViewModel(session, client);
            session.NotifyLibraryDataChanged();
            await WaitForAsync(() => viewModel.SelectedEntry != null);
            await WaitForAsync(() => viewModel.AddToCurrentProjectCommand.CanExecute(null));

            var dialogInvoked = false;
            viewModel.AddToCurrentProjectDialogHandler = dialogViewModel =>
            {
                dialogInvoked = true;
                dialogViewModel.SelectedParent = null;
                return true;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            await WaitForAsync(() => dialogInvoked);
            await WaitForAsync(() => viewModel.StatusMessage == "Current PDM Project does not contain a valid target parent Part. Analyze or open a valid PDM Project first.");

            Assert.False(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "library-references.json")));
            Assert.Equal(1, client.ResolveUsingStoredPolicyCallCount);
        }

        [Fact]
        public async Task AddToCurrentProject_AcceptedDialogWithInvalidQuantity_IsRejectedSafely()
        {
            using var folder = new TempWorkspaceFolder();
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                }, folder.Path)
            };
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", "Engineering Part Library", true) },
                SearchResponseToReturn = CreateSearchResponse("entry-1", "lib-1", "P-001", "Released", LibraryRevisionPolicy.LatestReleased),
                EntryDetailsToReturn = new PartLibraryEntryDetails
                {
                    EntryId = "entry-1",
                    LibraryId = "lib-1",
                    LibraryName = "Engineering Part Library",
                    PartId = "part-1",
                    PartConfigId = "cfg-1",
                    PartNumber = "P-001",
                    PartName = "Test Part",
                    Revision = "A",
                    RevisionPolicy = LibraryRevisionPolicy.LatestReleased,
                    PrimaryCadFileName = "P-001.ics",
                    CanAddToProject = true,
                    ResolutionFailed = false
                },
                ResolveResultToReturn = new ResolveLibraryPartResult
                {
                    ResolvedPartId = "part-1",
                    ResolvedPartConfigId = "cfg-1",
                    ResolvedRevision = "A"
                }
            };
            client.SearchResponseToReturn.Entries[0].CanAddToProject = true;
            client.SearchResponseToReturn.Entries[0].ResolutionFailed = false;

            var viewModel = new LibraryViewModel(session, client);
            session.NotifyLibraryDataChanged();
            await WaitForAsync(() => viewModel.SelectedEntry != null);
            await WaitForAsync(() => viewModel.AddToCurrentProjectCommand.CanExecute(null));

            var dialogInvoked = false;
            viewModel.AddToCurrentProjectDialogHandler = dialogViewModel =>
            {
                dialogInvoked = true;
                dialogViewModel.Quantity = "0";
                return true;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            await WaitForAsync(() => dialogInvoked);
            await WaitForAsync(() => viewModel.StatusMessage == "Select a target parent and enter a quantity greater than 0.");

            Assert.False(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "library-references.json")));
            Assert.Equal(1, client.ResolveUsingStoredPolicyCallCount);
        }

        [Fact]
        public async Task AddToCurrentProject_ValidSelection_AddsExactlyOneReference()
        {
            using var folder = new TempWorkspaceFolder();
            var session = new FakeAppSessionContext
            {
                CurrentUserName = "designer",
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                }, folder.Path)
            };
            var client = CreateReusableClient();
            var viewModel = new LibraryViewModel(session, client);
            await WaitForAsync(() => viewModel.SelectedEntry != null);
            await WaitForAsync(() => viewModel.AddToCurrentProjectCommand.CanExecute(null));

            var dialogInvoked = false;
            viewModel.AddToCurrentProjectDialogHandler = _ =>
            {
                dialogInvoked = true;
                return true;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            var referencePath = Path.Combine(folder.Path, ".idea-pdm", "library-references.json");
            await WaitForAsync(() => dialogInvoked);
            await WaitForAsync(() => File.Exists(referencePath));

            var store = new WorkspaceLibraryReferenceStore(new WorkspaceService(new WorkspaceOptions()));
            var reference = Assert.Single(store.Load(folder.Path));
            Assert.Equal("entry-1", reference.LibraryEntryId);
            Assert.Equal("part-1", reference.PartId);
            Assert.Equal("ROOT", reference.ParentLogicalCode);
            Assert.Equal(1, reference.Quantity);
            Assert.Equal("designer", reference.AddedBy);
            Assert.Equal(1, client.ResolveUsingStoredPolicyCallCount);
        }

        [Fact]
        public async Task AddToCurrentProject_CancelledDialog_DoesNotAddReference()
        {
            using var folder = new TempWorkspaceFolder();
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                }, folder.Path)
            };
            var client = CreateReusableClient();
            var viewModel = new LibraryViewModel(session, client);
            await WaitForAsync(() => viewModel.SelectedEntry != null);
            await WaitForAsync(() => viewModel.AddToCurrentProjectCommand.CanExecute(null));

            var dialogInvoked = false;
            viewModel.AddToCurrentProjectDialogHandler = _ =>
            {
                dialogInvoked = true;
                return false;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            await WaitForAsync(() => dialogInvoked);
            Assert.False(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "library-references.json")));
            Assert.Equal(1, client.ResolveUsingStoredPolicyCallCount);
        }

        [Fact]
        public async Task AddToCurrentProject_ReferenceStoreThrows_ConvertsExceptionToStatus()
        {
            using var folder = new TempWorkspaceFolder();
            var session = new FakeAppSessionContext
            {
                CurrentPdmProjectsViewModel = CreateWorkspaceModel(new[]
                {
                    new PdmStructureNode("Root", "ROOT", "Assembly", 1, "A", "Released", "blue")
                }, folder.Path)
            };
            var client = CreateReusableClient();
            var viewModel = new LibraryViewModel(session, client);
            await WaitForAsync(() => viewModel.SelectedEntry != null);
            await WaitForAsync(() => viewModel.AddToCurrentProjectCommand.CanExecute(null));
            viewModel.AddLibraryReferenceHandler = (_, __) =>
                throw new IOException("Simulated reference store failure.");

            var dialogInvoked = false;
            viewModel.AddToCurrentProjectDialogHandler = _ =>
            {
                dialogInvoked = true;
                return true;
            };

            viewModel.AddToCurrentProjectCommand.Execute(null);

            await WaitForAsync(() => dialogInvoked);
            await WaitForAsync(() =>
                viewModel.StatusMessage?.IndexOf(
                    "Failed to add Library Part to the current PDM Project",
                    StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.Equal(1, client.ResolveUsingStoredPolicyCallCount);
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

        private static StubPartLibraryClient CreateReusableClient()
        {
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", "Engineering Part Library", true) },
                SearchResponseToReturn = CreateSearchResponse(
                    "entry-1",
                    "lib-1",
                    "P-001",
                    "Released",
                    LibraryRevisionPolicy.Pinned),
                EntryDetailsToReturn = new PartLibraryEntryDetails
                {
                    EntryId = "entry-1",
                    LibraryId = "lib-1",
                    LibraryName = "Engineering Part Library",
                    PartId = "part-1",
                    PartConfigId = "cfg-1",
                    PartNumber = "P-001",
                    PartName = "Test Part",
                    Revision = "A",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    PrimaryCadFileName = "P-001.ics",
                    CanAddToProject = true,
                    ResolutionFailed = false
                },
                ResolveResultToReturn = new ResolveLibraryPartResult
                {
                    ResolvedPartId = "part-1",
                    ResolvedPartConfigId = "cfg-1",
                    ResolvedRevision = "A"
                }
            };
            client.SearchResponseToReturn.Entries[0].CanAddToProject = true;
            client.SearchResponseToReturn.Entries[0].ResolutionFailed = false;
            return client;
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

        private static PdmProjectsViewModel CreateWorkspaceModel(IEnumerable<PdmStructureNode> roots, string folderPath = null)
        {
            var workspace = new PdmProjectsViewModel();
            workspace.FolderPath = folderPath ?? CreateTempWorkspaceFolder();
            workspace.PdmStructure.Clear();

            foreach (var root in roots ?? Array.Empty<PdmStructureNode>())
            {
                workspace.PdmStructure.Add(root);
            }

            return workspace;
        }

        private static string CreateTempWorkspaceFolder()
        {
            var path = Path.Combine(Path.GetTempPath(), "IdeaCadConnector.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class TempWorkspaceFolder : IDisposable
        {
            public TempWorkspaceFolder()
            {
                Path = CreateTempWorkspaceFolder();
            }

            public string Path { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                        Directory.Delete(Path, true);
                }
                catch
                {
                }
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
            public int ResolveUsingStoredPolicyCallCount { get; private set; }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(
                LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active,
                CancellationToken cancellationToken = default)
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

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new MoveLibraryEntryResult { Success = true, EntryId = request?.EntryId, TargetLibraryId = request?.TargetLibraryId });

            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult(ResolveResultToReturn);

            public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(WhereUsedToReturn);

            public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new RecordLibraryUsageResult { Success = true });

            public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
            {
                ResolveUsingStoredPolicyCallCount++;
                return Task.FromResult(ResolveResultToReturn);
            }

            public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartRevisionHistoryResponse { Items = Array.Empty<PartRevisionHistoryItem>(), PageNumber = request?.PageNumber ?? 1, PageSize = request?.PageSize ?? 25, TotalCount = 0 });

            public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new UpdateLibraryRevisionPolicyResult());

            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = "lib-created" });

            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = request?.LibraryId });

            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = libraryId });

            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartPickerSearchResponse
                {
                    Items = Array.Empty<PartPickerSearchResultItem>(),
                    TotalCount = 0,
                    PageNumber = request?.PageNumber ?? 1,
                    PageSize = request?.PageSize ?? 25
                });

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(new PartPreview { PartId = partId, IsEligibleForReuse = true });

            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
                => Task.FromResult(new DuplicateEntryCheckResult { IsDuplicate = false });

            public void Dispose()
            {
            }
        }
    }
}
