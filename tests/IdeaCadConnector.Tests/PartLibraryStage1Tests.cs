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
    public class PartLibraryStage1Tests
    {
        [Fact]
        public async Task ResolvePartAsync_ExplicitPinned_DoesNotUseStoredLatestReleasedPolicy()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1", "pinned-1"));
            fake.ApplyItemResults.Enqueue(Part("pinned-1", "cfg-1", "PIN-001", "A", "Preliminary"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("released-1", "cfg-1", "REL-001", "B", "Released", "2")));

            var client = CreateClient(fake);

            var result = await client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.Pinned, CancellationToken.None);

            Assert.Equal("pinned-1", result.ResolvedPartId);
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.ItemId == "related-1");
        }

        [Fact]
        public async Task ResolvePartAsync_ExplicitLatestReleased_DoesNotUseStoredPinnedPolicy()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "pinned-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("released-2", "cfg-1", "REL-002", "B", "Released", "2")));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("released-2", "cfg-1", "REL-002", "B", "Released", "2")));

            var client = CreateClient(fake);

            var result = await client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.LatestReleased, CancellationToken.None);

            Assert.Equal("released-2", result.ResolvedPartId);
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.ItemId == "pinned-1");
        }

        [Fact]
        public async Task ResolvePartAsync_LatestReleasedWithoutReleasedRevision_FailsClearly()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.LatestReleased, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.Contains("No released revision is available", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResolvePartAsync_PinnedWithoutPinnedPartId_FailsClearly()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1"));

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.Pinned, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.Contains("pinned_part_id", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ResolvePartAsync_LatestCurrentWithoutCurrentRevision_DoesNotFallBackToRelatedPart()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "related-1"));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.LatestCurrent, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.ItemId == "related-1");
            Assert.Contains("current", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchEntriesAsync_LatestReleasedWithoutReleasedRevision_DoesNotSilentlyUseRelatedPart()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Entry("entry-1", "LatestReleased", "cfg-1", "related-1")));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.SearchEntriesAsync(new PartLibrarySearchRequest(), CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.ItemId == "related-1");
        }

        [Fact]
        public async Task UpdateRevisionPolicyAsync_Pinned_WritesPinnedFields()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(Part("pinned-1", "cfg-1", "PIN-001", "B", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var result = await client.UpdateRevisionPolicyAsync(
                new UpdateLibraryRevisionPolicyRequest
                {
                    EntryId = "entry-1",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    PinnedPartId = "pinned-1"
                },
                CancellationToken.None);

            var editCall = fake.CallLog.Last(call => call.MethodKind == "ApplyAml" && call.Action == "edit");
            Assert.True(result.Success);
            Assert.Contains("<revision_policy>Pinned</revision_policy>", editCall.AmlBody, StringComparison.Ordinal);
            Assert.Contains("<pinned_part_id>pinned-1</pinned_part_id>", editCall.AmlBody, StringComparison.Ordinal);
            Assert.Contains("<pinned_revision>B</pinned_revision>", editCall.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UpdateRevisionPolicyAsync_LatestReleased_ClearsPinnedFields()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "pinned-1"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("released-2", "cfg-1", "REL-002", "B", "Released", "2")));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("released-2", "cfg-1", "REL-002", "B", "Released", "2")));

            var client = CreateClient(fake);

            var result = await client.UpdateRevisionPolicyAsync(
                new UpdateLibraryRevisionPolicyRequest
                {
                    EntryId = "entry-1",
                    RevisionPolicy = LibraryRevisionPolicy.LatestReleased
                },
                CancellationToken.None);

            var editCall = fake.CallLog.First(call => call.MethodKind == "ApplyAml" && call.Action == "edit");
            Assert.True(result.Success);
            Assert.Contains("<revision_policy>LatestReleased</revision_policy>", editCall.AmlBody, StringComparison.Ordinal);
            Assert.Contains("<pinned_part_id is_null=\"1\" />", editCall.AmlBody, StringComparison.Ordinal);
            Assert.Contains("<pinned_revision is_null=\"1\" />", editCall.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetEntryAsync_PrefersLifecycleStateOverEntryStatus()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Published"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released", "1")));

            var client = CreateClient(fake);

            var result = await client.GetEntryAsync("entry-1", CancellationToken.None);

            Assert.Equal(LibraryEntryStatus.Published, result.EntryStatus);
            Assert.Equal("Published", result.LifecycleState);
        }

        [Fact]
        public async Task RecordUsageAsync_PrefersServerMethod_WhenAvailable()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodResults.Enqueue(new JObject
            {
                ["usage_id"] = "usage-1",
                ["usage_count"] = "2",
                ["last_used_on"] = "2026-07-04T10:00:00"
            });

            var client = CreateClient(fake);

            await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add");
        }

        [Fact]
        public async Task RecordUsageAsync_MissingServerMethod_FallsBackExactlyOnce()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodExceptions.Enqueue(new ArasOperationException(ArasErrorCode.ValidationFailed, "Method idea_RecordPartLibraryUsage not found"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "usage-type-id", ["name"] = PartLibrarySchemaNames.UsageItemType }));
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "usage-1" });
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "entry-1" });

            var client = CreateClient(fake);

            await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.Equal(1, fake.CallLog.Count(call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add"));
        }

        [Fact]
        public async Task GetWhereUsedAsync_CombinesBomAndLibraryUsage()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["source_id"] = "parent-1",
                ["related_id"] = "part-1",
                ["quantity"] = "2"
            }));
            fake.ApplyItemResults.Enqueue(Part("parent-1", "cfg-parent-1", "PARENT-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "usage-1",
                ["library_entry_id"] = "entry-1",
                ["part_id"] = "part-1",
                ["project_code"] = "PRJ-1",
                ["parent_part_id"] = "parent-2",
                ["quantity"] = "3",
                ["used_by"] = "tester",
                ["commit_id"] = "commit-1",
                ["action_type"] = "ReusedFromLibrary",
                ["created_on"] = "2026-07-04T10:00:00"
            }));
            fake.ApplyItemResults.Enqueue(Part("parent-2", "cfg-parent-2", "PARENT-002", "B", "Released"));

            var client = CreateClient(fake);

            var result = await client.GetWhereUsedAsync("part-1", CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, item => item.Source == WhereUsedSource.Bom && item.ParentPartId == "parent-1");
            Assert.Contains(result, item => item.Source == WhereUsedSource.LibraryUsage && item.ProjectCode == "PRJ-1" && item.ParentPartId == "parent-2");
        }

        private static HttpPartLibraryClient CreateClient(FakeArasAmlClient fake)
        {
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "testdb" };
            return new HttpPartLibraryClient(options, fake, NullLogger<HttpPartLibraryClient>.Instance);
        }

        private static LibraryUsageRequest MakeUsageRequest()
        {
            return new LibraryUsageRequest
            {
                LibraryEntryId = "entry-1",
                PartId = "part-1",
                ProjectCode = "TEST",
                ParentPartId = "parent-1",
                Quantity = 1,
                UsedBy = "tester",
                CommitId = "commit-1",
                ActionType = "ReusedFromLibrary"
            };
        }

        private static void EnqueueSchema(FakeArasAmlClient fake)
        {
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "it-lib", ["name"] = PartLibrarySchemaNames.LibraryItemType }));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "it-entry", ["name"] = PartLibrarySchemaNames.EntryRelationshipType }));
        }

        private static JObject Entry(
            string id,
            string revisionPolicy,
            string configId,
            string relatedId,
            string pinnedPartId = null,
            string entryStatus = "Draft",
            string state = null)
        {
            return new JObject
            {
                ["id"] = id,
                ["source_id"] = "lib-1",
                ["related_id"] = relatedId,
                ["part_config_id"] = configId,
                ["revision_policy"] = revisionPolicy,
                ["pinned_part_id"] = pinnedPartId,
                ["pinned_revision"] = pinnedPartId == null ? null : "A",
                ["entry_status"] = entryStatus,
                ["state"] = state
            };
        }

        private static JObject Library(string id, string name)
        {
            return new JObject
            {
                ["id"] = id,
                ["name"] = name,
                ["status"] = "Active",
                ["library_type"] = "Standard",
                ["is_public"] = "1"
            };
        }

        private static JObject Part(
            string id,
            string configId,
            string itemNumber,
            string majorRev,
            string state,
            string generation = "1")
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
                ["generation"] = generation
            };
        }

        private static JObject Items(params JObject[] items)
        {
            return new JObject
            {
                ["Items"] = new JArray(items ?? Array.Empty<JObject>())
            };
        }
    }
}
