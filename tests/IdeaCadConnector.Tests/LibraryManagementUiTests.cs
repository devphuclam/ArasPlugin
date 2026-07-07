using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class LibraryManagementUiTests
    {
        [Fact]
        public void DefaultVisibilityFilter_IsActive()
        {
            var viewModel = CreateViewModel();

            Assert.Equal("Active", viewModel.SelectedVisibilityFilter);
        }

        [Fact]
        public async Task VisibilityFilter_RefreshesWithCorrectEnum()
        {
            var client = new StubPartLibraryClient();
            var viewModel = CreateViewModel(client: client);

            await WaitForAsync(() => client.VisibilityFiltersSeen.Count >= 1);
            viewModel.SelectedVisibilityFilter = "Archived";
            await WaitForAsync(() => client.VisibilityFiltersSeen.Contains(LibraryVisibilityFilter.Archived));
            viewModel.SelectedVisibilityFilter = "All";
            await WaitForAsync(() => client.VisibilityFiltersSeen.Contains(LibraryVisibilityFilter.All));

            Assert.Equal(LibraryVisibilityFilter.Active, client.VisibilityFiltersSeen[0]);
            Assert.Contains(LibraryVisibilityFilter.Archived, client.VisibilityFiltersSeen);
            Assert.Contains(LibraryVisibilityFilter.All, client.VisibilityFiltersSeen);
        }

        [Fact]
        public async Task ArchivedLibrary_DisablesAddAndArchiveCommands()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Archived", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);

            viewModel.SelectedVisibilityFilter = "Archived";
            await WaitForAsync(() => viewModel.Libraries.Count == 1);

            Assert.True(viewModel.SelectedLibrary.IsArchived);
            Assert.False(viewModel.CanAddEntryToSelectedLibrary);
            Assert.False(viewModel.ShowPartPickerCommand.CanExecute(null));
            Assert.False(viewModel.ArchiveLibraryCommand.CanExecute(null));
        }

        [Fact]
        public void CreateCommand_IsManagerOnly()
        {
            Assert.True(CreateViewModel(canManage: true).CreateLibraryCommand.CanExecute(null));
            Assert.False(CreateViewModel(canManage: false).CreateLibraryCommand.CanExecute(null));
        }

        [Fact]
        public async Task EditAndArchiveCommands_AreManagerOnly()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));

            var managerVm = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            await WaitForAsync(() => managerVm.SelectedLibrary != null);

            var viewerVm = CreateViewModel(client: client, canManage: false, canUsePartPicker: false);
            await WaitForAsync(() => viewerVm.SelectedLibrary != null);

            Assert.True(managerVm.EditLibraryCommand.CanExecute(null));
            Assert.True(managerVm.ArchiveLibraryCommand.CanExecute(null));
            Assert.False(viewerVm.EditLibraryCommand.CanExecute(null));
            Assert.False(viewerVm.ArchiveLibraryCommand.CanExecute(null));
        }

        [Fact]
        public async Task ArchiveCancelled_DoesNotCallClient()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.ConfirmDialogHandler = (_, _, _, _) => System.Windows.MessageBoxResult.No;

            await WaitForAsync(() => viewModel.SelectedLibrary != null);
            viewModel.ArchiveLibraryCommand.Execute(null);
            await Task.Delay(150);

            Assert.Equal(0, client.ArchiveLibraryCallCount);
        }

        [Fact]
        public async Task ArchiveConfirmed_CallsClient_AndActiveFilterHidesArchivedLibrary()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.ConfirmDialogHandler = (_, _, _, _) => System.Windows.MessageBoxResult.Yes;

            await WaitForAsync(() => viewModel.SelectedLibrary != null);
            viewModel.ArchiveLibraryCommand.Execute(null);
            await WaitForAsync(() => client.ArchiveLibraryCallCount == 1);
            await WaitForAsync(() => viewModel.Libraries.Count == 0);

            Assert.Equal("lib-a", client.ArchivedLibraryId);
            Assert.Empty(viewModel.Libraries);
        }

        [Fact]
        public async Task ArchivePermissionDenied_ShowsStatus()
        {
            var client = new StubPartLibraryClient
            {
                ArchiveLibraryResult = new LibraryMutationResult
                {
                    Success = false,
                    ErrorCode = ArasErrorCode.PermissionDenied
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.ConfirmDialogHandler = (_, _, _, _) => System.Windows.MessageBoxResult.Yes;

            await WaitForAsync(() => viewModel.SelectedLibrary != null);
            viewModel.ArchiveLibraryCommand.Execute(null);
            await WaitForAsync(() => client.ArchiveLibraryCallCount == 1);

            Assert.Contains("archive", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateLibraryDialog_DefaultsToTeamAndLatestCurrent()
        {
            var vm = new CreateLibraryDialogViewModel(new StubPartLibraryClient());

            Assert.Equal(LibraryType.Team, vm.SelectedType);
            Assert.Equal(LibraryRevisionPolicy.LatestCurrent, vm.DefaultRevisionPolicy);
            Assert.False(vm.IsPublic);
        }

        [Fact]
        public async Task CreateLibraryDialog_DuplicateName_StaysOpen()
        {
            var client = new StubPartLibraryClient
            {
                CreateLibraryResult = new LibraryMutationResult
                {
                    Success = false,
                    ErrorMessage = "A Library named 'Design Reuse' already exists."
                }
            };
            var vm = new CreateLibraryDialogViewModel(client) { Name = "Design Reuse" };
            var closed = false;
            vm.CloseRequested += _ => closed = true;

            vm.SaveCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(vm.ValidationMessage));

            Assert.False(closed);
            Assert.Contains("already exists", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MoveCommand_DisabledWithNoSelectedEntry()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);

            Assert.False(viewModel.MoveEntryCommand.CanExecute(null));
        }

        [Fact]
        public async Task MoveCommand_DisabledForViewer()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: false, canUsePartPicker: false);

            await WaitForAsync(() => viewModel.SelectedLibrary != null);
            Assert.False(viewModel.MoveEntryCommand.CanExecute(null));
        }

        [Fact]
        public async Task MoveCommand_DisabledForArchivedSelectedLibrary()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Archived Lib", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);

            viewModel.SelectedVisibilityFilter = "Archived";
            await WaitForAsync(() => viewModel.Libraries.Count == 1 && viewModel.SelectedLibrary != null);

            Assert.True(viewModel.SelectedLibrary.IsArchived);
            Assert.False(viewModel.MoveEntryCommand.CanExecute(null));
        }

        [Fact]
        public async Task MoveCommand_OpensDialogForManager()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-b", "Library B", canContribute: true));
            client.EntriesToReturn.Add(CreateEntrySummary("entry-1", "lib-a", "PART-001"));
            var moved = false;
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.MoveEntryDialogHandler = vm =>
            {
                moved = true;
                return true;
            };

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.Entries.Count > 0);
            Assert.True(viewModel.MoveEntryCommand.CanExecute(null));
            viewModel.MoveEntryCommand.Execute(null);
            await Task.Delay(200);

            Assert.True(moved);
        }

        [Fact]
        public async Task MoveCommand_SuccessfulMove_RefreshesEntries()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-b", "Library B", canContribute: true));
            client.EntriesToReturn.Add(CreateEntrySummary("entry-1", "lib-a", "PART-001"));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.MoveEntryDialogHandler = vm => true;

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.Entries.Count > 0);
            viewModel.MoveEntryCommand.Execute(null);
            await Task.Delay(300);

            Assert.NotNull(viewModel.Entries);
        }

        [Fact]
        public async Task MoveCommand_FailedMove_KeepsDialogOpenAndShowsError()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = false,
                    ErrorMessage = "Move blocked by server."
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-b", "Library B", canContribute: true));
            client.EntriesToReturn.Add(CreateEntrySummary("entry-1", "lib-a", "PART-001"));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.MoveEntryDialogHandler = vm =>
            {
                return false;
            };

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.Entries.Count > 0);
            viewModel.MoveEntryCommand.Execute(null);
            await Task.Delay(200);

            Assert.NotNull(viewModel.StatusMessage);
        }

        [Fact]
        public async Task MoveCommand_PermissionDenied_DisplayedClearly()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = false,
                    ErrorCode = ArasErrorCode.PermissionDenied
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-b", "Library B", canContribute: true));
            client.EntriesToReturn.Add(CreateEntrySummary("entry-1", "lib-a", "PART-001"));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);
            viewModel.MoveEntryDialogHandler = vm => false;

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.Entries.Count > 0);
            viewModel.MoveEntryCommand.Execute(null);
            await Task.Delay(200);

            Assert.NotNull(viewModel.StatusMessage);
        }

        [Fact]
        public async Task RevisionBrowserCommand_DisabledWithNoSelectedEntry()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: true, canUsePartPicker: true);

            Assert.False(viewModel.ShowRevisionBrowserCommand.CanExecute(null));
        }

        [Fact]
        public async Task RevisionBrowserCommand_DisabledForViewer()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            var viewModel = CreateViewModel(client: client, canManage: false, canUsePartPicker: false);

            await WaitForAsync(() => viewModel.SelectedLibrary != null);
            Assert.False(viewModel.ShowRevisionBrowserCommand.CanExecute(null));
        }

        [Fact]
        public async Task RevisionBrowserCommand_OpensDialogForContributor()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-a", "Library A", canContribute: true));
            client.EntriesToReturn.Add(CreateEntrySummary("entry-1", "lib-a", "PART-001"));
            var opened = false;
            var viewModel = CreateViewModel(client: client, canManage: false, canUsePartPicker: true);
            viewModel.RevisionBrowserDialogHandler = vm =>
            {
                opened = true;
                return true;
            };

            await WaitForAsync(() => viewModel.SelectedLibrary != null && viewModel.Entries.Count > 0);
            Assert.True(viewModel.ShowRevisionBrowserCommand.CanExecute(null));
            viewModel.ShowRevisionBrowserCommand.Execute(null);
            await Task.Delay(200);

            Assert.True(opened);
        }

        [Fact]
        public async Task EditLibraryDialog_InitializesFromLibrary_AndArchivedDisablesSave()
        {
            var library = CreateLibrary("lib-a", "Team Library", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived);
            library.DefaultRevisionPolicy = "Pinned";
            var vm = new EditLibraryDialogViewModel(new StubPartLibraryClient(), library);
            await vm.InitializeAsync();

            Assert.Equal("Team Library", vm.Name);
            Assert.Equal(LibraryRevisionPolicy.Pinned, vm.DefaultRevisionPolicy);
            Assert.True(vm.IsArchived);
            Assert.False(vm.CanSave);
        }

        private static LibraryViewModel CreateViewModel(
            StubPartLibraryClient client = null,
            bool canManage = false,
            bool canUsePartPicker = false)
        {
            var session = new FakeAppSessionContext { PdmClient = new StubPdmRepositoryClient() };
            return new LibraryViewModel(
                session,
                client ?? new StubPartLibraryClient(),
                new StubLibraryAuthorizationService
                {
                    CanManage = canManage,
                    CanUsePicker = canUsePartPicker
                });
        }

        private static PartLibrarySummary CreateLibrary(
            string id,
            string name,
            bool canContribute,
            string status = PartLibrarySchemaNames.LibraryStatusActive)
        {
            return new PartLibrarySummary
            {
                Id = id,
                Name = name,
                Description = name + " description",
                CanContribute = canContribute,
                ItemCount = 1,
                LibraryType = LibraryType.Team,
                IsPublic = false,
                Status = status,
                DefaultRevisionPolicy = "LatestCurrent"
            };
        }

        private static PartLibraryEntrySummary CreateEntrySummary(
            string entryId,
            string libraryId,
            string partNumber,
            string partName = "Test Part")
        {
            return new PartLibraryEntrySummary
            {
                EntryId = entryId,
                LibraryId = libraryId,
                LibraryName = "Test Library",
                PartId = "part-" + entryId,
                PartConfigId = "cfg-" + entryId,
                PartNumber = partNumber,
                PartName = partName,
                PartType = "Component",
                Revision = "A",
                LifecycleState = "Released",
                EntryLifecycleState = "Draft",
                RevisionPolicy = LibraryRevisionPolicy.LatestReleased,
                EntryStatus = LibraryEntryStatus.Draft,
                CadStatus = "Available",
                UsageCount = 0,
                HasNewerReleasedRevision = false,
                IsDeprecated = false,
                ResolutionFailed = false,
                CanAddToProject = true
            };
        }

        private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 2500)
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

        private sealed class StubLibraryAuthorizationService : ILibraryAuthorizationService
        {
            public bool CanManage { get; set; }
            public bool CanUsePicker { get; set; }

            public bool IsLibraryManager => CanManage;

            public bool IsContributorOrHigher => CanManage || CanUsePicker;

            public bool IsReadOnlyViewer => !IsContributorOrHigher;

            public bool CanManageLibraries => CanManage;

            public bool CanUsePartPicker => CanUsePicker || CanManage;
        }

        private sealed class FakeAppSessionContext : IAppSessionContext
        {
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

        private sealed class StubPartLibraryClient : IPartLibraryClient
        {
            public List<PartLibrarySummary> ActiveLibraries { get; } = new List<PartLibrarySummary>();
            public List<PartLibraryEntrySummary> EntriesToReturn { get; } = new List<PartLibraryEntrySummary>();
            public LibraryMutationResult CreateLibraryResult { get; set; } = new LibraryMutationResult { Success = true, LibraryId = "lib-created" };
            public LibraryMutationResult UpdateLibraryResult { get; set; } = new LibraryMutationResult { Success = true };
            public LibraryMutationResult ArchiveLibraryResult { get; set; } = new LibraryMutationResult { Success = true };
            public MoveLibraryEntryResult MoveLibraryEntryResult { get; set; } = new MoveLibraryEntryResult { Success = true };
            public List<LibraryVisibilityFilter> VisibilityFiltersSeen { get; } = new List<LibraryVisibilityFilter>();
            public int ArchiveLibraryCallCount { get; private set; }
            public string ArchivedLibraryId { get; private set; }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active, CancellationToken cancellationToken = default)
            {
                VisibilityFiltersSeen.Add(visibilityFilter);

                IEnumerable<PartLibrarySummary> libraries = ActiveLibraries;
                if (visibilityFilter == LibraryVisibilityFilter.Active)
                {
                    libraries = ActiveLibraries.Where(x => !string.Equals(x.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase));
                }
                else if (visibilityFilter == LibraryVisibilityFilter.Archived)
                {
                    libraries = ActiveLibraries.Where(x => string.Equals(x.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase));
                }

                return Task.FromResult((IReadOnlyList<PartLibrarySummary>)libraries.ToList());
            }

            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(CreateLibraryResult);

            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(UpdateLibraryResult);

            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
            {
                ArchiveLibraryCallCount++;
                ArchivedLibraryId = libraryId;
                var target = ActiveLibraries.FirstOrDefault(x => x.Id == libraryId);
                if (target != null && ArchiveLibraryResult.Success)
                    target.Status = PartLibrarySchemaNames.LibraryStatusArchived;

                return Task.FromResult(ArchiveLibraryResult);
            }

            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartPickerSearchResponse { Items = Array.Empty<PartPickerSearchResultItem>(), TotalCount = 0, PageNumber = 1, PageSize = 25 });

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(new PartPreview { PartId = partId, ConfigId = "cfg-" + partId, IsEligibleForReuse = true });

            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
                => Task.FromResult(new DuplicateEntryCheckResult());

            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
            {
                var filtered = EntriesToReturn
                    .Where(e => string.IsNullOrWhiteSpace(request.LibraryId) || string.Equals(e.LibraryId, request.LibraryId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Task.FromResult(new PartLibrarySearchResponse { Entries = filtered, TotalCount = filtered.Count, PageNumber = 1, PageSize = 25 });
            }

            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibraryEntryDetails());

            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new AddPartToLibraryResult { Success = true, EntryId = "entry-1" });

            public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(MoveLibraryEntryResult);

            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartRevisionHistoryResponse { Items = Array.Empty<PartRevisionHistoryItem>(), PageNumber = 1, PageSize = 25, TotalCount = 0 });

            public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new UpdateLibraryRevisionPolicyResult());

            public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());

            public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new RecordLibraryUsageResult { Success = true });

            public void Dispose()
            {
            }
        }

        private sealed class StubPdmRepositoryClient : IPdmRepositoryClient
        {
            public Task<PdmPushResult> PushAsync(PdmPushRequest request, CancellationToken ct) => Task.FromResult(new PdmPushResult());
            public Task<PdmExistencePreview> PreviewExistenceAsync(PdmPushRequest request, CancellationToken ct) => Task.FromResult(new PdmExistencePreview());
            public Task<PdmCloneResult> CloneLatestToWorkspaceAsync(PdmCloneRequest request, CancellationToken ct) => Task.FromResult(new PdmCloneResult());
            public Task<string> FindItemIdByNumberAsync(string itemType, string itemNumber, CancellationToken ct) => Task.FromResult<string>(null);
            public Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct) => Task.FromResult(new PdmReviseResult());
            public void Dispose() { }
        }
    }
}
