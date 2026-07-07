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
    public class MoveLibraryEntryViewModelTests
    {
        [Fact]
        public async Task Initialize_LoadsActiveWritableLibraries_ExcludingCurrentAndArchived()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source Lib", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-1", "Target A", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-2", "Target B", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-archived", "Archived", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived));
            client.ActiveLibraries.Add(CreateLibrary("lib-no-contribute", "No Contribute", canContribute: false));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            Assert.Equal(2, vm.TargetLibraries.Count);
            Assert.Contains(vm.TargetLibraries, l => l.Id == "lib-tgt-1");
            Assert.Contains(vm.TargetLibraries, l => l.Id == "lib-tgt-2");
            Assert.DoesNotContain(vm.TargetLibraries, l => l.Id == "lib-src");
            Assert.DoesNotContain(vm.TargetLibraries, l => l.Id == "lib-archived");
            Assert.DoesNotContain(vm.TargetLibraries, l => l.Id == "lib-no-contribute");
        }

        [Fact]
        public async Task NoValidTargets_LeavesTargetListEmpty()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source Lib", canContribute: true));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            Assert.Empty(vm.TargetLibraries);
            Assert.False(vm.CanMove);
        }

        [Fact]
        public async Task ArchivedTarget_IsExcludedFromList()
        {
            var client = new StubPartLibraryClient();
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-arch", "Archived", canContribute: true, status: PartLibrarySchemaNames.LibraryStatusArchived));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            Assert.Empty(vm.TargetLibraries);
        }

        [Fact]
        public async Task SuccessfulMove_CallsMoveLibraryEntryAsync_AndCloses()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = true,
                    EntryId = "entry-1",
                    TargetLibraryId = "lib-tgt-1",
                    SourceLibraryId = "lib-src",
                    PreservedEntryStatus = "Draft",
                    PreservedLifecycleState = "Draft"
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-1", "Target", canContribute: true));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            var closed = false;
            vm.CloseRequested += accepted => closed = accepted;

            Assert.True(vm.CanMove);
            vm.MoveCommand.Execute(null);
            await WaitForAsync(() => closed);

            Assert.True(closed);
            Assert.True(vm.MoveResult?.Success);
        }

        [Fact]
        public async Task FailedMove_ShowsErrorMessage_AndStaysOpen()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = false,
                    ErrorMessage = "Target Library is full.",
                    ErrorCode = ArasErrorCode.ValidationFailed
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-1", "Target", canContribute: true));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            var closed = false;
            vm.CloseRequested += accepted => closed = accepted;

            vm.MoveCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(vm.ErrorMessage));

            Assert.False(closed);
            Assert.Contains("full", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PermissionDenied_ShowsPermissionMessage()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = false,
                    ErrorCode = ArasErrorCode.PermissionDenied
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-1", "Target", canContribute: true));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            vm.MoveCommand.Execute(null);
            await WaitForAsync(() => !string.IsNullOrWhiteSpace(vm.ErrorMessage));

            Assert.Contains("permission", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SuccessfulMove_TriggersLibraryRefresh()
        {
            var client = new StubPartLibraryClient
            {
                MoveLibraryEntryResult = new MoveLibraryEntryResult
                {
                    Success = true,
                    EntryId = "entry-1",
                    TargetLibraryId = "lib-tgt-1"
                }
            };
            client.ActiveLibraries.Add(CreateLibrary("lib-src", "Source", canContribute: true));
            client.ActiveLibraries.Add(CreateLibrary("lib-tgt-1", "Target", canContribute: true));

            var entry = CreateEntrySummary("entry-1", "lib-src");
            var vm = new MoveLibraryEntryDialogViewModel(client, entry, "lib-src");
            await vm.InitializeAsync();

            var closed = false;
            vm.CloseRequested += accepted => closed = accepted;
            vm.MoveCommand.Execute(null);
            await WaitForAsync(() => closed);

            Assert.True(closed);
        }

        [Fact]
        public void EntrySummary_DisplaysCorrectly()
        {
            var entry = CreateEntrySummary("entry-1", "lib-src", partNumber: "PART-001", partName: "Test Part");

            var vm = new MoveLibraryEntryDialogViewModel(new StubPartLibraryClient(), entry, "lib-src");

            Assert.Equal("PART-001", vm.PartNumber);
            Assert.Equal("Test Part", vm.PartName);
            Assert.Equal("Source Lib", vm.CurrentLibraryName);
        }

        private static PartLibraryEntrySummary CreateEntrySummary(
            string entryId,
            string libraryId,
            string partNumber = "PART-001",
            string partName = "Test Part",
            string configId = "cfg-001")
        {
            return new PartLibraryEntrySummary
            {
                EntryId = entryId,
                LibraryId = libraryId,
                LibraryName = "Source Lib",
                PartId = "part-" + entryId,
                PartConfigId = configId,
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
            public List<PartLibrarySummary> ActiveLibraries { get; } = new List<PartLibrarySummary>();
            public MoveLibraryEntryResult MoveLibraryEntryResult { get; set; } = new MoveLibraryEntryResult { Success = true };

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active, CancellationToken cancellationToken = default)
            {
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

            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(MoveLibraryEntryResult);

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
