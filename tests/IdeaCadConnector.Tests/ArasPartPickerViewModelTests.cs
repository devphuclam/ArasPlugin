using System;
using System.Collections.Generic;
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
    public class ArasPartPickerViewModelTests
    {
        [Fact]
        public async Task InitializeAsync_LoadsOnlyWritableActiveLibraries()
        {
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[]
                {
                    CreateLibrary("lib-1", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusActive),
                    CreateLibrary("lib-2", canContribute: false, status: PartLibrarySchemaNames.LibraryStatusActive),
                    CreateLibrary("lib-3", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived)
                }
            };

            var viewModel = new ArasPartPickerViewModel(client);
            await viewModel.InitializeAsync();

            Assert.Single(viewModel.Libraries);
            Assert.Equal("lib-1", viewModel.Libraries[0].Id);
        }

        [Fact]
        public async Task Search_PassesFilters_AndCapsPageSizeTo100()
        {
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", true, PartLibrarySchemaNames.LibraryStatusActive) }
            };
            var viewModel = new ArasPartPickerViewModel(client)
            {
                Keyword = "iron",
                PartType = "Mechanical/Part",
                LifecycleState = "Released",
                MajorRev = "A",
                CurrentOnly = true,
                SelectedPageSize = 200
            };

            await viewModel.InitializeAsync();
            viewModel.SearchCommand.Execute(null);
            await WaitForAsync(() => client.LastSearchRequest != null);

            Assert.Equal("iron", client.LastSearchRequest.Keyword);
            Assert.Equal("Mechanical/Part", client.LastSearchRequest.PartType);
            Assert.Equal("Released", client.LastSearchRequest.LifecycleState);
            Assert.Equal("A", client.LastSearchRequest.MajorRev);
            Assert.True(client.LastSearchRequest.CurrentOnly);
            Assert.Equal(100, client.LastSearchRequest.PageSize);
        }

        [Fact]
        public async Task Search_WithNoResults_ShowsFriendlyState()
        {
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", true, PartLibrarySchemaNames.LibraryStatusActive) },
                SearchPartsResult = new PartPickerSearchResponse
                {
                    Items = Array.Empty<PartPickerSearchResultItem>(),
                    TotalCount = 0,
                    PageNumber = 1,
                    PageSize = 25
                }
            };

            var viewModel = new ArasPartPickerViewModel(client);
            await viewModel.InitializeAsync();
            viewModel.SearchCommand.Execute(null);
            await WaitForAsync(() => viewModel.HasSearched);

            Assert.Empty(viewModel.SearchResults);
            Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        }

        [Fact]
        public async Task SelectingPart_LoadsPreview()
        {
            var client = CreateReadyClient();
            var viewModel = new ArasPartPickerViewModel(client);
            await viewModel.InitializeAsync();
            viewModel.SearchCommand.Execute(null);
            await WaitForAsync(() => viewModel.SearchResults.Count == 1);

            viewModel.SelectedPart = viewModel.SearchResults[0];
            await WaitForAsync(() => viewModel.PartPreview != null);

            Assert.Equal("cfg-1", viewModel.PartPreview.ConfigId);
            Assert.True(viewModel.PartPreview.IsEligibleForReuse);
        }

        [Fact]
        public async Task MissingConfigId_BlocksAdd()
        {
            var client = CreateReadyClient();
            client.PartPreviewResult = new PartPreview
            {
                PartId = "part-1",
                ConfigId = "",
                IsEligibleForReuse = true
            };

            var viewModel = await CreateReadyViewModelAsync(client);
            viewModel.AddCommand.Execute(null);
            await Task.Delay(100);

            Assert.Contains("config", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, client.AddPartCallCount);
        }

        [Fact]
        public async Task IneligiblePreview_BlocksAdd()
        {
            var client = CreateReadyClient();
            client.PartPreviewResult = new PartPreview
            {
                PartId = "part-1",
                ConfigId = "cfg-1",
                IsEligibleForReuse = false,
                IneligibilityReason = "Part is not eligible for reuse."
            };

            var viewModel = await CreateReadyViewModelAsync(client);
            viewModel.AddCommand.Execute(null);
            await Task.Delay(100);

            Assert.Contains("eligible", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, client.AddPartCallCount);
        }

        [Fact]
        public async Task DuplicateEntry_BlocksAddBeforeMutation()
        {
            var client = CreateReadyClient();
            client.DuplicateCheckResult = new DuplicateEntryCheckResult
            {
                IsDuplicate = true,
                ExistingEntryId = "entry-42"
            };

            var viewModel = await CreateReadyViewModelAsync(client);
            viewModel.AddCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(viewModel.StatusMessage));

            Assert.Contains("entry-42", viewModel.StatusMessage);
            Assert.Equal(0, client.AddPartCallCount);
        }

        [Fact]
        public async Task ArchivedTarget_BlocksAdd()
        {
            var client = CreateReadyClient();
            var viewModel = await CreateReadyViewModelAsync(client);
            viewModel.TargetLibrary = CreateLibrary("lib-arch", true, PartLibrarySchemaNames.LibraryStatusArchived);

            viewModel.AddCommand.Execute(null);
            await Task.Delay(100);

            Assert.Contains("archived", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, client.AddPartCallCount);
        }

        [Fact]
        public async Task AddSuccess_ClosesDialog_AndCallsClient()
        {
            var client = CreateReadyClient();
            var viewModel = await CreateReadyViewModelAsync(client);
            var closed = false;
            viewModel.CloseRequested += accepted => closed = accepted;

            viewModel.AddCommand.Execute(null);
            await WaitForAsync(() => closed);

            Assert.True(closed);
            Assert.Equal(1, client.AddPartCallCount);
        }

        [Fact]
        public async Task AlreadyExists_DoesNotCloseDialog()
        {
            var client = CreateReadyClient();
            client.AddPartResult = new AddPartToLibraryResult
            {
                Success = true,
                AlreadyExists = true,
                EntryId = "entry-existing"
            };
            var viewModel = await CreateReadyViewModelAsync(client);
            var closed = false;
            viewModel.CloseRequested += _ => closed = true;

            viewModel.AddCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(viewModel.StatusMessage));

            Assert.False(closed);
            Assert.Contains("entry-existing", viewModel.StatusMessage);
        }

        [Fact]
        public async Task PermissionDeniedDuringSearch_ShowsClearError()
        {
            var client = new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", true, PartLibrarySchemaNames.LibraryStatusActive) },
                SearchException = new ArasOperationException(ArasErrorCode.PermissionDenied, "Forbidden")
            };

            var viewModel = new ArasPartPickerViewModel(client);
            await viewModel.InitializeAsync();
            viewModel.SearchCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(viewModel.ErrorMessage));

            Assert.Contains("permission", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        private static StubPartLibraryClient CreateReadyClient()
        {
            return new StubPartLibraryClient
            {
                LibrariesToReturn = new[] { CreateLibrary("lib-1", true, PartLibrarySchemaNames.LibraryStatusActive) },
                SearchPartsResult = new PartPickerSearchResponse
                {
                    Items = new[]
                    {
                        new PartPickerSearchResultItem
                        {
                            PartId = "part-1",
                            ConfigId = "cfg-1",
                            PartNumber = "P-001",
                            Name = "Part 1",
                            PartType = "Mechanical/Part",
                            MajorRev = "A",
                            Generation = "3",
                            LifecycleState = "Released",
                            IsCurrent = true,
                            IsReleased = true,
                            CadStatus = "Healthy"
                        }
                    },
                    TotalCount = 1,
                    PageNumber = 1,
                    PageSize = 25
                },
                PartPreviewResult = new PartPreview
                {
                    PartId = "part-1",
                    ConfigId = "cfg-1",
                    Revision = "A",
                    LifecycleState = "Released",
                    Generation = "3",
                    CadStatus = "Healthy",
                    IsEligibleForReuse = true
                }
            };
        }

        private static async Task<ArasPartPickerViewModel> CreateReadyViewModelAsync(StubPartLibraryClient client)
        {
            var viewModel = new ArasPartPickerViewModel(client);
            await viewModel.InitializeAsync();
            viewModel.SearchCommand.Execute(null);
            await WaitForAsync(() => viewModel.SearchResults.Count == 1);
            viewModel.SelectedPart = viewModel.SearchResults[0];
            await WaitForAsync(() => viewModel.PartPreview != null);
            return viewModel;
        }

        private static PartLibrarySummary CreateLibrary(string id, bool canContribute, string status)
        {
            return new PartLibrarySummary
            {
                Id = id,
                Name = id,
                CanContribute = canContribute,
                ItemCount = 1,
                LibraryType = LibraryType.Team,
                IsPublic = false,
                Status = status,
                DefaultRevisionPolicy = "LatestCurrent"
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
            public IReadOnlyList<PartLibrarySummary> LibrariesToReturn { get; set; } = Array.Empty<PartLibrarySummary>();
            public PartPickerSearchResponse SearchPartsResult { get; set; } = new PartPickerSearchResponse { Items = Array.Empty<PartPickerSearchResultItem>(), TotalCount = 0, PageNumber = 1, PageSize = 25 };
            public PartPreview PartPreviewResult { get; set; } = new PartPreview();
            public DuplicateEntryCheckResult DuplicateCheckResult { get; set; } = new DuplicateEntryCheckResult();
            public AddPartToLibraryResult AddPartResult { get; set; } = new AddPartToLibraryResult { Success = true, EntryId = "entry-1" };
            public PartPickerSearchRequest LastSearchRequest { get; private set; }
            public int AddPartCallCount { get; private set; }
            public Exception SearchException { get; set; }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active, CancellationToken cancellationToken = default)
                => Task.FromResult(LibrariesToReturn);

            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
            {
                LastSearchRequest = request;
                if (SearchException != null)
                    throw SearchException;

                return Task.FromResult(SearchPartsResult);
            }

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult(PartPreviewResult);

            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
                => Task.FromResult(DuplicateCheckResult);

            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
            {
                AddPartCallCount++;
                return Task.FromResult(AddPartResult);
            }

            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = "lib-created" });

            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = request.LibraryId });

            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult { Success = true, LibraryId = libraryId });

            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibrarySearchResponse { Entries = Array.Empty<PartLibraryEntrySummary>(), TotalCount = 0, PageNumber = 1, PageSize = 25 });

            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibraryEntryDetails());

            public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new MoveLibraryEntryResult { Success = true, EntryId = request?.EntryId, TargetLibraryId = request?.TargetLibraryId });

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
    }
}
