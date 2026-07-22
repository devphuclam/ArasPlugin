using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PartLibraryStateProviderTests
    {
        [Fact]
        public async Task GetPartStateAsync_ReturnsAuthoritativeLifecycleState()
        {
            var client = new FakePartLibraryClient
            {
                Preview = new PartPreview { LifecycleState = "Part Review" }
            };
            var provider = new PartLibraryStateProvider(() => client);

            var result = await provider.GetPartStateAsync("part-1", CancellationToken.None);

            Assert.Equal("Part Review", result);
            Assert.Equal("part-1", client.LastPartId);
            Assert.Equal(1, client.GetPartPreviewCallCount);
        }

        [Fact]
        public async Task GetPartStateAsync_ReturnsNullWhenClientUnavailable()
        {
            var provider = new PartLibraryStateProvider(() => null);

            var result = await provider.GetPartStateAsync("part-1", CancellationToken.None);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task GetPartStateAsync_DoesNotCallClientForMissingPartId(string partId)
        {
            var client = new FakePartLibraryClient();
            var provider = new PartLibraryStateProvider(() => client);

            var result = await provider.GetPartStateAsync(partId, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, client.GetPartPreviewCallCount);
        }

        [Fact]
        public async Task GetPartStateAsync_PropagatesAuthorityFailure()
        {
            var client = new FakePartLibraryClient
            {
                PreviewException = new InvalidOperationException("authority unavailable")
            };
            var provider = new PartLibraryStateProvider(() => client);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.GetPartStateAsync("part-1", CancellationToken.None));

            Assert.Equal("authority unavailable", exception.Message);
        }

        private sealed class FakePartLibraryClient : IPartLibraryClient
        {
            public PartPreview Preview { get; set; }
            public Exception PreviewException { get; set; }
            public string LastPartId { get; private set; }
            public int GetPartPreviewCallCount { get; private set; }

            public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
            {
                LastPartId = partId;
                GetPartPreviewCallCount++;
                if (PreviewException != null)
                    throw PreviewException;
                return Task.FromResult(Preview);
            }

            public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active, CancellationToken cancellationToken = default(CancellationToken))
                => Task.FromResult((IReadOnlyList<PartLibrarySummary>)Array.Empty<PartLibrarySummary>());
            public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult());
            public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult());
            public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
                => Task.FromResult(new LibraryMutationResult());
            public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new MoveLibraryEntryResult());
            public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartPickerSearchResponse());
            public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartRevisionHistoryResponse());
            public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
                => Task.FromResult(new DuplicateEntryCheckResult());
            public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibrarySearchResponse());
            public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new PartLibraryEntryDetails());
            public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new AddPartToLibraryResult());
            public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());
            public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
                => Task.FromResult(new ResolveLibraryPartResult());
            public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new UpdateLibraryRevisionPolicyResult());
            public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
                => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());
            public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
                => Task.FromResult(new RecordLibraryUsageResult());
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

            public void Dispose()
            {
            }
        }
    }
}
