using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PartLibraryStage2CoreTests
    {
        [Fact]
        public async Task MoveLibraryEntryAsync_MissingEntryId_ReturnsValidationFailed()
        {
            var client = CreateClient(new FakeArasAmlClient());

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_MissingTargetLibraryId_ReturnsValidationFailed()
        {
            var client = CreateClient(new FakeArasAmlClient());

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_ArchivedTargetBlocksMoveWithoutMutating()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-src", "cfg-1", "Draft", "Draft", "latest", "A", "B", "Cat", "tag", "note", "proj", "commit"));
            fake.ApplyItemResults.Enqueue(Library("lib-target", "Target", PartLibrarySchemaNames.LibraryStatusArchived));

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Equal(0, fake.CountAmlCalls("idea_PartLibraryEntry", "edit"));
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_DuplicateActiveTargetBlocksMove()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-src", "cfg-1", "Draft", "Draft", "latest", "A", "B", "Cat", "tag", "note", "proj", "commit"));
            fake.ApplyItemResults.Enqueue(Library("lib-target", "Target"));
            fake.ApplyItemResults.Enqueue(Library("lib-src", "Source"));
            fake.ApplyAmlResults.Enqueue(Items(Entry("dup-1", "lib-target", "cfg-1", "Published", "Published", "LatestReleased", "part-dup", "A", "Cat", "tag", "note", "proj", "commit")));

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
            Assert.Contains("same part_config_id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_DeprecatedDuplicateDoesNotBlockMove()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-src", "cfg-1", "Draft", "Draft", "latest", "A", "B", "Cat", "tag", "note", "proj", "commit"));
            fake.ApplyItemResults.Enqueue(Library("lib-target", "Target"));
            fake.ApplyItemResults.Enqueue(Library("lib-src", "Source"));
            fake.ApplyAmlResults.Enqueue(Items(Entry("dup-1", "lib-target", "cfg-1", "Deprecated", "Deprecated", "LatestReleased", "part-dup", "A", "Cat", "tag", "note", "proj", "commit")));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-target", "cfg-1", "Draft", "Draft", "latest", "A", "B", "Cat", "tag", "note", "proj", "commit"));

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("lib-src", result.SourceLibraryId);
            Assert.Equal("lib-target", result.TargetLibraryId);
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_SameLibraryIsNoOp()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-src", "cfg-1", "Draft", "Draft", "latest", "A", "B", "Cat", "tag", "note", "proj", "commit"));

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-src" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("lib-src", result.SourceLibraryId);
            Assert.Equal("lib-src", result.TargetLibraryId);
            Assert.Equal(0, fake.CountAmlCalls("idea_PartLibraryEntry", "edit"));
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_SuccessPreservesMetadata()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-src", "cfg-1", "Draft", "Draft", "LatestCurrent", "P-001", "P-001-A", "Cat", "tag", "note", "proj", "commit"));
            fake.ApplyItemResults.Enqueue(Library("lib-target", "Target"));
            fake.ApplyItemResults.Enqueue(Library("lib-src", "Source"));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "lib-target", "cfg-1", "Draft", "Draft", "LatestCurrent", "P-001", "P-001-A", "Cat", "tag", "note", "proj", "commit"));

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Draft", result.PreservedEntryStatus);
            Assert.Equal("Draft", result.PreservedLifecycleState);
            Assert.Contains(fake.Calls, call => call.MethodKind == "ApplyAml" && call.Action == "edit" && call.ItemType == PartLibrarySchemaNames.EntryRelationshipType);
        }

        [Theory]
        [InlineData(ArasErrorCode.PermissionDenied)]
        [InlineData(ArasErrorCode.AuthInvalid)]
        [InlineData(ArasErrorCode.AuthExpired)]
        [InlineData(ArasErrorCode.ServerUnavailable)]
        public async Task MoveLibraryEntryAsync_AuthAndServerErrorsPropagateAsResult(ArasErrorCode errorCode)
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, PartLibrarySchemaNames.EntryRelationshipType, StringComparison.OrdinalIgnoreCase) && string.Equals(action, "get", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(errorCode, "Blocked")
                    : null;

            var client = CreateClient(fake);

            var result = await client.MoveLibraryEntryAsync(
                new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(errorCode, result.ErrorCode);
        }

        [Fact]
        public async Task MoveLibraryEntryAsync_OperationCanceledExceptionPropagates()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            var client = CreateClient(fake);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                client.MoveLibraryEntryAsync(
                    new MoveLibraryEntryRequest { EntryId = "entry-1", TargetLibraryId = "lib-target" },
                    cts.Token));
        }

        [Fact]
        public async Task SearchPartRevisionsAsync_ResolvesConfigFromPartId()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "P-001", "A", "Released", "2", "2026-07-06T10:00:00", true));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "P-001", "A", "Released", "2", "2026-07-06T10:00:00", true)));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "P-001", "A", "Released", "2", "2026-07-06T10:00:00", true)));

            var client = CreateClient(fake);

            var result = await client.SearchPartRevisionsAsync(
                new PartRevisionHistoryRequest { PartId = "part-1", PageSize = 25, PageNumber = 1 },
                CancellationToken.None);

            Assert.Equal("cfg-1", result.Items.Single().ConfigId);
            Assert.Equal(1, fake.Calls.Count(call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.Action == "get"));
            Assert.Equal(3, fake.CountAmlCalls("Part", "get"));
        }

        [Fact]
        public async Task SearchPartRevisionsAsync_MissingIdentifiersReturnValidationFailed()
        {
            var client = CreateClient(new FakeArasAmlClient());

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.SearchPartRevisionsAsync(new PartRevisionHistoryRequest(), CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task SearchPartRevisionsAsync_UsesPagingAndCapsPageSize()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "P-001", "A", "Released")));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "P-001", "A", "Released")));

            var client = CreateClient(fake);

            var result = await client.SearchPartRevisionsAsync(
                new PartRevisionHistoryRequest { PartConfigId = "cfg-1", PageSize = 500, PageNumber = 2 },
                CancellationToken.None);

            Assert.Equal(100, result.PageSize);
            Assert.Equal(2, result.PageNumber);
            var pageCall = fake.Calls.Last(call => call.MethodKind == "ApplyAml" && call.ItemType == "Part");
            Assert.Contains("pagesize=\"100\"", pageCall.AmlBody, StringComparison.Ordinal);
            Assert.Contains("page=\"2\"", pageCall.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchPartRevisionsAsync_SortsDeterministicallyAndMarksPinEligibility()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-3", "cfg-1", "P-003", "10", "Released", "10", "2026-07-06T10:00:00", true),
                Part("part-1", "cfg-1", "P-001", "1", "Released", "1", "2026-07-05T10:00:00", true),
                Part("part-2", "cfg-1", "P-002", "2", "Obsolete", "2", "2026-07-04T10:00:00", false),
                Part("part-4", string.Empty, "P-004", string.Empty, "Released", "3", "2026-07-03T10:00:00", false)));
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-3", "cfg-1", "P-003", "10", "Released", "10", "2026-07-06T10:00:00", true),
                Part("part-1", "cfg-1", "P-001", "1", "Released", "1", "2026-07-05T10:00:00", true),
                Part("part-2", "cfg-1", "P-002", "2", "Obsolete", "2", "2026-07-04T10:00:00", false),
                Part("part-4", string.Empty, "P-004", string.Empty, "Released", "3", "2026-07-03T10:00:00", false)));

            var client = CreateClient(fake);

            var result = await client.SearchPartRevisionsAsync(
                new PartRevisionHistoryRequest { PartConfigId = "cfg-1", PageSize = 25 },
                CancellationToken.None);

            Assert.Collection(result.Items,
                first => Assert.Equal("part-3", first.PartId),
                second =>
                {
                    Assert.Equal("part-4", second.PartId);
                    Assert.False(second.CanPin);
                    Assert.Contains("config_id", second.CannotPinReason, StringComparison.OrdinalIgnoreCase);
                },
                third =>
                {
                    Assert.Equal("part-2", third.PartId);
                    Assert.True(third.IsObsolete);
                    Assert.False(third.CanPin);
                },
                fourth =>
                {
                    Assert.Equal("part-1", fourth.PartId);
                    Assert.True(fourth.CanPin);
                });
        }

        [Fact]
        public async Task SearchPartRevisionsAsync_NestedItemsDoNotCreatePhantomRows()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "P-001", "A", "Released"),
                new JObject { ["type"] = "Part CAD", ["id"] = "nested-1", ["item_number"] = "CAD-1" }));
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "P-001", "A", "Released"),
                new JObject { ["type"] = "Part CAD", ["id"] = "nested-1", ["item_number"] = "CAD-1" }));

            var client = CreateClient(fake);

            var result = await client.SearchPartRevisionsAsync(
                new PartRevisionHistoryRequest { PartConfigId = "cfg-1" },
                CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal("part-1", result.Items[0].PartId);
        }

        private static HttpPartLibraryClient CreateClient(FakeArasAmlClient fake)
        {
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "testdb" };
            return new HttpPartLibraryClient(options, fake, NullLogger<HttpPartLibraryClient>.Instance);
        }

        private static void EnqueueSchema(FakeArasAmlClient fake)
        {
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "it-lib", ["name"] = PartLibrarySchemaNames.LibraryItemType }));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "it-entry", ["name"] = PartLibrarySchemaNames.EntryRelationshipType }));
        }

        private static JObject Items(params JObject[] items)
        {
            return new JObject
            {
                ["Items"] = new JArray(items ?? Array.Empty<JObject>())
            };
        }

        private static JObject Library(string id, string name, string status = "Active")
        {
            return new JObject
            {
                ["id"] = id,
                ["name"] = name,
                ["status"] = status,
                ["library_type"] = "Team",
                ["is_public"] = "1"
            };
        }

        private static JObject Entry(
            string id,
            string sourceId,
            string configId,
            string entryStatus,
            string state,
            string revisionPolicy,
            string pinnedPartId,
            string pinnedRevision,
            string category,
            string tags,
            string note,
            string sourceProject,
            string sourceCommit)
        {
            return new JObject
            {
                ["id"] = id,
                ["source_id"] = sourceId,
                ["related_id"] = "part-" + id,
                ["part_config_id"] = configId,
                ["entry_status"] = entryStatus,
                ["state"] = state,
                ["revision_policy"] = revisionPolicy,
                ["pinned_part_id"] = pinnedPartId,
                ["pinned_revision"] = pinnedRevision,
                ["category"] = category,
                ["tags"] = tags,
                ["note"] = note,
                ["source_project"] = sourceProject,
                ["source_commit"] = sourceCommit
            };
        }

        private static JObject Part(
            string id,
            string configId,
            string itemNumber,
            string majorRev,
            string state,
            string generation = "1",
            string modifiedOn = "2026-07-06T08:00:00",
            bool isCurrent = true)
        {
            return new JObject
            {
                ["id"] = id,
                ["config_id"] = configId,
                ["item_number"] = itemNumber,
                ["name"] = itemNumber + "-Name",
                ["classification"] = "Component",
                ["major_rev"] = majorRev,
                ["state"] = state,
                ["generation"] = generation,
                ["modified_on"] = modifiedOn,
                ["created_on"] = modifiedOn,
                ["is_current"] = isCurrent ? "1" : "0"
            };
        }
    }
}
