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
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class PartRevisionBrowserViewModelTests
    {
        [Fact]
        public async Task Initialize_CallsSearchPartRevisionsAsync()
        {
            var client = new StubPartLibraryClient();
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            Assert.True(vm.HasLoaded);
            Assert.Equal(1, client.SearchPartRevisionsCallCount);
        }

        [Fact]
        public async Task PageSizeRequest_CappedTo100()
        {
            var client = new StubPartLibraryClient();
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            vm.SelectedPageSize = 500;
            await Task.Delay(150);

            Assert.Equal(100, client.LastPageSizeRequested);
        }

        [Fact]
        public async Task EmptyRevisions_ShowsFriendlyState()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = new PartRevisionHistoryResponse
                {
                    Items = Array.Empty<PartRevisionHistoryItem>(),
                    PageNumber = 1,
                    PageSize = 25,
                    TotalCount = 0
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            Assert.Empty(vm.Revisions);
            Assert.Contains("No revisions", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CanPin_False_DisablesPin()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-1", CanPin = false, CannotPinReason = "Obsolete revision" })
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();

            Assert.NotNull(vm.SelectedRevision);
            Assert.False(vm.CanPin);
            Assert.False(vm.PinCommand.CanExecute(null));
        }

        [Fact]
        public async Task CanPin_True_EnablesPin_ForReviewer()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-2", CanPin = true })
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: true);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();

            Assert.NotNull(vm.SelectedRevision);
            Assert.True(vm.CanPin);
            Assert.True(vm.PinCommand.CanExecute(null));
        }

        [Fact]
        public async Task CanPin_False_WhenCanPinRevisionsIsFalse()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-2b", CanPin = true })
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: false);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();

            Assert.NotNull(vm.SelectedRevision);
            Assert.False(vm.CanPin);
            Assert.False(vm.PinCommand.CanExecute(null));
        }

        [Fact]
        public async Task Pin_DoesNotCallUpdate_WhenCanPinRevisionsIsFalse()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-2c", CanPin = true }),
                UpdateRevisionPolicyResult = new UpdateLibraryRevisionPolicyResult
                {
                    Success = true,
                    EntryId = "entry-1",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: false);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();
            vm.PinCommand.Execute(null);
            await Task.Delay(150);

            Assert.Null(client.LastUpdateRevisionPolicyRequest);
            Assert.False(vm.PinSuccess);
        }

        [Fact]
        public async Task Pin_CallsUpdateRevisionPolicyAsync_WithPinnedAndSelectedPartId()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-3", CanPin = true }),
                UpdateRevisionPolicyResult = new UpdateLibraryRevisionPolicyResult
                {
                    Success = true,
                    EntryId = "entry-1",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    ResolvedPartId = "part-3"
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: true);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();
            Assert.NotNull(vm.SelectedRevision);

            vm.PinCommand.Execute(null);
            await WaitForAsync(() => vm.PinSuccess);

            Assert.True(vm.PinSuccess);
            Assert.NotNull(client.LastUpdateRevisionPolicyRequest);
            Assert.Equal(LibraryRevisionPolicy.Pinned, client.LastUpdateRevisionPolicyRequest.RevisionPolicy);
            Assert.Equal("part-3", client.LastUpdateRevisionPolicyRequest.PinnedPartId);
            Assert.Equal("entry-1", client.LastUpdateRevisionPolicyRequest.EntryId);
        }

        [Fact]
        public async Task PinSuccess_RefreshesEntry()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-4", CanPin = true }),
                UpdateRevisionPolicyResult = new UpdateLibraryRevisionPolicyResult
                {
                    Success = true,
                    EntryId = "entry-1",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: true);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();
            vm.PinCommand.Execute(null);
            await WaitForAsync(() => vm.PinSuccess);

            Assert.True(vm.PinSuccess);
            Assert.Contains("pinned", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PinFailure_DisplaysError_AndKeepsDialogOpen()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = CreateResponse(
                    new PartRevisionHistoryItem { PartId = "part-5", CanPin = true }),
                UpdateRevisionPolicyResult = new UpdateLibraryRevisionPolicyResult
                {
                    Success = false,
                    ErrorMessage = "Cannot pin an obsolete revision."
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased", canPinRevisions: true);
            await vm.InitializeAsync();

            vm.SelectedRevision = vm.Revisions.FirstOrDefault();
            vm.PinCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(vm.ErrorMessage));

            Assert.False(vm.PinSuccess);
            Assert.Contains("Cannot pin", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PermissionDenied_DisplayedClearly()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsException = new ArasOperationException(ArasErrorCode.PermissionDenied, "Access denied")
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            Assert.Contains("permission", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NoConfigId_ShowsValidationMessage()
        {
            var client = new StubPartLibraryClient();
            var entry = CreateEntryRow("entry-1", configId: null);

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            Assert.True(vm.HasNoConfigId);
            Assert.Contains("Config ID", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, client.SearchPartRevisionsCallCount);
        }

        [Fact]
        public async Task Paging_UpdatesShowPageText()
        {
            var client = new StubPartLibraryClient
            {
                SearchPartRevisionsResult = new PartRevisionHistoryResponse
                {
                    Items = new List<PartRevisionHistoryItem>
                    {
                        new PartRevisionHistoryItem { PartId = "part-1", CanPin = true }
                    },
                    PageNumber = 2,
                    PageSize = 25,
                    TotalCount = 50
                }
            };
            var entry = CreateEntryRow("entry-1", configId: "cfg-001");

            var vm = new PartRevisionBrowserViewModel(client, entry, "LatestReleased");
            await vm.InitializeAsync();

            Assert.Contains("2", vm.ShowPageText);
            Assert.Contains("50", vm.ShowPageText);
        }

        [Fact]
        public void EntrySummary_DisplaysCorrectly()
        {
            var entry = CreateEntryRow("entry-1", configId: "cfg-001", partNumber: "PART-X", partName: "Part X");

            var vm = new PartRevisionBrowserViewModel(new StubPartLibraryClient(), entry, "Pinned");

            Assert.Equal("PART-X", vm.PartNumber);
            Assert.Equal("Part X", vm.PartName);
            Assert.Equal("cfg-001", vm.ConfigId);
            Assert.Equal("Pinned", vm.CurrentRevisionPolicy);
        }

        private static PartLibraryEntryRow CreateEntryRow(
            string entryId,
            string configId,
            string partNumber = "PART-001",
            string partName = "Test Part")
        {
            return new PartLibraryEntryRow
            {
                EntryId = entryId,
                LibraryId = "lib-1",
                PartId = "part-" + entryId,
                PartConfigId = configId,
                PartNumber = partNumber,
                PartName = partName,
                PartType = "Component",
                Revision = "A",
                LifecycleState = "Released",
                EntryLifecycleState = "Draft",
                EntryStatus = "Draft",
                RevisionPolicy = "LatestReleased",
                CadStatus = "Available",
                UsageCount = 0,
                HasNewerReleasedRevision = false,
                IsDeprecated = false,
                ResolutionFailed = false,
                CanAddToProject = true,
                LibraryName = "Test Library"
            };
        }

        private static PartRevisionHistoryResponse CreateResponse(params PartRevisionHistoryItem[] items)
        {
            return new PartRevisionHistoryResponse
            {
                Items = items?.ToList() ?? new List<PartRevisionHistoryItem>(),
                PageNumber = 1,
                PageSize = 25,
                TotalCount = items?.Length ?? 0
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

        private sealed class StubPartLibraryClient : IPartLibraryClient
        {
            public int SearchPartRevisionsCallCount { get; private set; }
            public int LastPageSizeRequested { get; private set; }
            public PartRevisionHistoryResponse SearchPartRevisionsResult { get; set; } = new PartRevisionHistoryResponse { Items = Array.Empty<PartRevisionHistoryItem>(), PageNumber = 1, PageSize = 25, TotalCount = 0 };
            public Exception SearchPartRevisionsException { get; set; }
            public UpdateLibraryRevisionPolicyResult UpdateRevisionPolicyResult { get; set; } = new UpdateLibraryRevisionPolicyResult { Success = true };
            public UpdateLibraryRevisionPolicyRequest LastUpdateRevisionPolicyRequest { get; private set; }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active, CancellationToken cancellationToken = default)
                => Task.FromResult((IReadOnlyList<PartLibrarySummary>)Array.Empty<PartLibrarySummary>());

            public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
            {
                SearchPartRevisionsCallCount++;
                LastPageSizeRequested = request?.PageSize ?? 0;
                if (SearchPartRevisionsException != null)
                    return Task.FromException<PartRevisionHistoryResponse>(SearchPartRevisionsException);

                return Task.FromResult(SearchPartRevisionsResult);
            }

            public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
            {
                LastUpdateRevisionPolicyRequest = request;
                return Task.FromResult(UpdateRevisionPolicyResult);
            }

            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartPickerSearchResponse { Items = Array.Empty<PartPickerSearchResultItem>(), TotalCount = 0, PageNumber = 1, PageSize = 25 });

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(new PartPreview { PartId = partId, ConfigId = "cfg-" + partId, IsEligibleForReuse = true });

            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
                => Task.FromResult(new DuplicateEntryCheckResult());

            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibrarySearchResponse { Entries = Array.Empty<PartLibraryEntrySummary>(), TotalCount = 0, PageNumber = 1, PageSize = 25 });

            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibraryEntryDetails());

            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new AddPartToLibraryResult { Success = true, EntryId = "entry-1" });

            public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new MoveLibraryEntryResult { Success = true });

            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());

            public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());

            public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new RecordLibraryUsageResult { Success = true });

            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true });

            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true });

            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true });

            public Task<LibraryEntryCadDetails> GetCadDetailsAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryEntryCadDetails());
            public Task<LibraryEntryBomDetails> GetBomDetailsAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryEntryBomDetails());
            public Task<LibraryEntryRevisionDetails> GetRevisionDetailsAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryEntryRevisionDetails());
            public Task<LibraryEntryWhereUsedDetails> GetWhereUsedDetailsAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryEntryWhereUsedDetails());
            public Task<LibraryEntryDetailBundle> GetDetailBundleAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryEntryDetailBundle());

            public void Dispose() { }
        }
    }
}
