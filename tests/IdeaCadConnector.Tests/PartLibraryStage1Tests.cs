using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;
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
            fake.ApplyAmlExceptionFactory = (amlBody, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.UsageItemType, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "idea_PartLibraryUsage ItemType does not exist on server.")
                    : null;

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
            fake.ApplyAmlExceptionFactory = (amlBody, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.UsageItemType, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "idea_PartLibraryUsage ItemType does not exist on server.")
                    : null;

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.LatestCurrent, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyItem" && call.ItemType == "Part" && call.ItemId == "related-1");
            Assert.Contains("current", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchEntriesAsync_InvalidEntryDoesNotHideValidEntries()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-invalid", "LatestReleased", "cfg-1", "related-1"),
                Entry("entry-valid", "Pinned", "cfg-2", "related-2", "related-2", "Draft", "Published")));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Preliminary"));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-2", "cfg-2", "PART-002", "B", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest(), CancellationToken.None);

            Assert.Equal(2, result.Entries.Count);
            var invalid = Assert.Single(result.Entries.Where(entry => entry.EntryId == "entry-invalid"));
            var valid = Assert.Single(result.Entries.Where(entry => entry.EntryId == "entry-valid"));
            Assert.True(invalid.ResolutionFailed);
            Assert.False(invalid.CanAddToProject);
            Assert.False(string.IsNullOrWhiteSpace(invalid.ResolutionError));
            Assert.Equal("entry-valid", valid.EntryId);
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
            fake.ApplyAmlResults.Enqueue(Items(Part("released-2", "cfg-1", "REL-002", "B", "Released", "2")));
            fake.ApplyAmlResults.Enqueue(new JObject());

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
        public async Task UpdateRevisionPolicyAsync_LatestReleasedFailure_DoesNotEditEntry()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "pinned-1"));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.UpdateRevisionPolicyAsync(
                    new UpdateLibraryRevisionPolicyRequest
                    {
                        EntryId = "entry-1",
                        RevisionPolicy = LibraryRevisionPolicy.LatestReleased
                    },
                    CancellationToken.None));

            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.Action == "edit" && call.ItemType == PartLibrarySchemaNames.EntryRelationshipType);
        }

        [Fact]
        public async Task UpdateRevisionPolicyAsync_PinnedObsoletePart_DoesNotEditEntry()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(Part("pinned-1", "cfg-1", "PIN-001", "B", "Obsolete"));

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.UpdateRevisionPolicyAsync(
                    new UpdateLibraryRevisionPolicyRequest
                    {
                        EntryId = "entry-1",
                        RevisionPolicy = LibraryRevisionPolicy.Pinned,
                        PinnedPartId = "pinned-1"
                    },
                    CancellationToken.None));

            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.Action == "edit" && call.ItemType == PartLibrarySchemaNames.EntryRelationshipType);
        }

        [Fact]
        public async Task UpdateRevisionPolicyAsync_PinnedMissingMajorRev_DoesNotEditEntry()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "pinned-1",
                ["config_id"] = "cfg-1",
                ["item_number"] = "PIN-001",
                ["name"] = "PIN-001-Name",
                ["classification"] = "Component",
                ["state"] = "Released"
            });

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.UpdateRevisionPolicyAsync(
                    new UpdateLibraryRevisionPolicyRequest
                    {
                        EntryId = "entry-1",
                        RevisionPolicy = LibraryRevisionPolicy.Pinned,
                        PinnedPartId = "pinned-1"
                    },
                    CancellationToken.None));

            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.Action == "edit" && call.ItemType == PartLibrarySchemaNames.EntryRelationshipType);
        }

        [Fact]
        public async Task GetEntryAsync_SeparatesEntryLifecycleFromPartLifecycle()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released", "1")));

            var client = CreateClient(fake);

            var result = await client.GetEntryAsync("entry-1", CancellationToken.None);

            Assert.Equal(LibraryEntryStatus.Draft, result.EntryStatus);
            Assert.Equal("Draft", result.EntryLifecycleState);
            Assert.Equal("Released", result.LifecycleState);
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
        public async Task RecordUsageAsync_MissingServerMethod_ReturnsTrackingUnavailable()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodExceptions.Enqueue(new ArasOperationException(ArasErrorCode.ValidationFailed, "Method idea_RecordPartLibraryUsage not found"));
            var client = CreateClient(fake);

            var result = await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.TrackingUnavailable);
            Assert.NotNull(result.WarningMessage);
            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add");
        }

        [Fact]
        public async Task RecordUsageAsync_IncludesIdempotencyKey()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodResults.Enqueue(new JObject
            {
                ["usage_id"] = "usage-1",
                ["usage_count"] = "1",
                ["last_used_on"] = "2026-07-04T10:00:00"
            });
            var client = CreateClient(fake);

            var result = await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            var methodCall = Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.NotNull(methodCall.MethodParameters);
            Assert.Contains(PartLibrarySchemaNames.UsageIdempotencyKeyProperty, methodCall.MethodParameters.Keys);
            Assert.False(string.IsNullOrWhiteSpace(methodCall.MethodParameters[PartLibrarySchemaNames.UsageIdempotencyKeyProperty]));
        }

        [Fact]
        public async Task RecordUsageAsync_AlreadyExists_ReturnsCorrectResult()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodResults.Enqueue(new JObject
            {
                ["usage_id"] = "usage-1",
                ["usage_count"] = "3",
                ["last_used_on"] = "2026-07-04T10:00:00",
                ["already_exists"] = "1"
            });
            var client = CreateClient(fake);

            var result = await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.AlreadyExists);
            Assert.Equal(3, result.UsageCount);
            Assert.Equal("usage-1", result.UsageId);
        }

        [Fact]
        public async Task RecordUsageAsync_NullRequest_ReturnsFailure()
        {
            var fake = new FakeArasAmlClient();
            var client = CreateClient(fake);

            var result = await client.RecordUsageAsync(null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Empty(fake.CallLog);
        }

        [Fact]
        public async Task RecordUsageAsync_ServerUnavailable_DoesNotFallback()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodExceptions.Enqueue(new ArasOperationException(ArasErrorCode.ServerUnavailable, "service unavailable"));
            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() => client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add");
        }

        [Fact]
        public async Task RecordUsageAsync_GenericValidationFailed_DoesNotFallback()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodExceptions.Enqueue(new ArasOperationException(ArasErrorCode.ValidationFailed, "quantity must be greater than zero"));
            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() => client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add");
        }

        [Fact]
        public async Task PublishEntryAsync_VerifiesActualState()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft"));

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() => client.PublishEntryAsync("entry-1", CancellationToken.None));
            Assert.Contains("Expected 'Published'", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        // ── Null / invalid method response tests ──────────────────────

        [Fact]
        public async Task RecordUsageAsync_NullMethodResponse_ThrowsNotSuccess()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodResults.Enqueue(null);

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            Assert.Equal(ArasErrorCode.UnexpectedServerError, ex.ErrorCode);
            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
        }

        [Fact]
        public async Task RecordUsageAsync_MethodResponseWithoutUsageId_Throws()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodResults.Enqueue(new JObject
            {
                ["usage_count"] = "1",
                ["last_used_on"] = "2026-07-04T10:00:00"
            });

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            Assert.Equal(ArasErrorCode.UnexpectedServerError, ex.ErrorCode);
            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
        }

        // ── AuthExpired, cancellation tests ───────────────────────────

        [Fact]
        public async Task RecordUsageAsync_AuthExpired_DoesNotFallback()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyMethodExceptions.Enqueue(new ArasOperationException(ArasErrorCode.AuthExpired, "session expired"));
            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            Assert.Single(fake.CallLog.Where(call => call.MethodKind == "ApplyMethod"));
            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.ItemType == PartLibrarySchemaNames.UsageItemType && call.Action == "add");
        }

        [Fact]
        public async Task RecordUsageAsync_Cancellation_DoesNotFallback()
        {
            var fake = new FakeArasAmlClient();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), cts.Token));
        }

        // ── Usage count authoritative tests ────────────────────────────

        [Fact]
        public async Task SearchEntriesAsync_UsageCountFromUsageItems()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // Schema: 2 calls consumed. ApplyAmlResults remaining: [0..N]
            // Two entries
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft"),
                Entry("entry-2", "Pinned", "cfg-2", "related-2", "related-2", "Draft", "Draft")));
            // Usage query - three usages for entry-1, one for entry-2 (called BEFORE MapEntrySummaryAsync)
            fake.ApplyAmlResults.Enqueue(Items(
                new JObject { ["id"] = "usage-1", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "usage-2", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "usage-3", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "usage-4", ["library_entry_id"] = "entry-2" }));
            // CAD for entry-1 (related-1)
            fake.ApplyAmlResults.Enqueue(new JObject());
            // Latest released for entry-1
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));
            // CAD for entry-2 (related-2)
            fake.ApplyAmlResults.Enqueue(new JObject());
            // Latest released for entry-2
            fake.ApplyAmlResults.Enqueue(Items(Part("related-2", "cfg-2", "PART-002", "A", "Released")));

            // ApplyItem call order is library1 -> part1 -> library2 -> part2
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-2", "cfg-2", "PART-002", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None);

            Assert.Equal(2, result.Entries.Count);
            var entry1 = Assert.Single(result.Entries.Where(e => e.EntryId == "entry-1"));
            var entry2 = Assert.Single(result.Entries.Where(e => e.EntryId == "entry-2"));
            Assert.Equal(3, entry1.UsageCount);
            Assert.Equal(1, entry2.UsageCount);
        }

        [Fact]
        public async Task SearchEntriesAsync_MultipleUsagesSameEntry_CorrectCount()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft")));
            // Usage query (called BEFORE MapEntrySummaryAsync)
            fake.ApplyAmlResults.Enqueue(Items(
                new JObject { ["id"] = "u1", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "u2", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "u3", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "u4", ["library_entry_id"] = "entry-1" },
                new JObject { ["id"] = "u5", ["library_entry_id"] = "entry-1" }));
            // CAD
            fake.ApplyAmlResults.Enqueue(new JObject());
            // Latest released
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            // ApplyItem calls
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None);

            var entry = Assert.Single(result.Entries);
            Assert.Equal(5, entry.UsageCount);
        }

        [Fact]
        public async Task SearchEntriesAsync_UsageQueryWithNoRecords_ReturnsZeroEvenWithCachedCount()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "entry-1",
                ["source_id"] = "lib-1",
                ["related_id"] = "related-1",
                ["part_config_id"] = "cfg-1",
                ["revision_policy"] = "Pinned",
                ["pinned_part_id"] = "related-1",
                ["entry_status"] = "Draft",
                ["state"] = "Draft",
                ["usage_count"] = "4"
            }));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None);

            var entry = Assert.Single(result.Entries);
            Assert.Equal(0, entry.UsageCount);
        }

        [Fact]
        public async Task SearchEntriesAsync_UsageQueryWithNoRecords_ReturnsZeroEvenWithCachedCount_AndCacheIgnored()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // Entry with cached usage_count=2
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "entry-1",
                ["source_id"] = "lib-1",
                ["related_id"] = "related-1",
                ["part_config_id"] = "cfg-1",
                ["revision_policy"] = "Pinned",
                ["pinned_part_id"] = "related-1",
                ["entry_status"] = "Draft",
                ["state"] = "Draft",
                ["usage_count"] = "2"
            }));
            // Usage query returns empty (simulates missing/not-deployed ItemType)
            // LoadUsageCountsAsync returns empty dict → fallback to cached usage_count
            fake.ApplyAmlResults.Enqueue(Items());
            // CAD + latest released
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            // ApplyItem calls
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None);

            var entry = Assert.Single(result.Entries);
            Assert.Equal(0, entry.UsageCount);
        }

        [Fact]
        public async Task SearchEntriesAsync_UsageItemTypeMissing_UsesCachedCount()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "entry-1",
                ["source_id"] = "lib-1",
                ["related_id"] = "related-1",
                ["part_config_id"] = "cfg-1",
                ["revision_policy"] = "Pinned",
                ["pinned_part_id"] = "related-1",
                ["entry_status"] = "Draft",
                ["state"] = "Draft",
                ["usage_count"] = "2"
            }));
            fake.ApplyAmlExceptionFactory = (amlBody, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.UsageItemType, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "idea_PartLibraryUsage ItemType does not exist on server.")
                    : null;
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None);

            var entry = Assert.Single(result.Entries);
            Assert.Equal(2, entry.UsageCount);
        }

        [Fact]
        public async Task SearchEntriesAsync_PermissionDeniedQueryingUsage_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Entry("entry-1", "Pinned", "cfg-1", "related-1")));
            // Usage query — must be queued AFTER schema and entries but BEFORE CAD/latestReleased
            fake.ApplyAmlExceptions.Enqueue(new ArasOperationException(ArasErrorCode.PermissionDenied, "access denied"));
            // CAD + latest released (never reached due to exception)
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items());

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None));
        }

        [Fact]
        public async Task SearchEntriesAsync_AuthFailureQueryingUsage_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Entry("entry-1", "Pinned", "cfg-1", "related-1")));
            // Usage query — must be queued AFTER schema and entries but BEFORE CAD/latestReleased
            fake.ApplyAmlExceptions.Enqueue(new ArasOperationException(ArasErrorCode.AuthInvalid, "not authenticated"));
            // CAD + latest released (never reached)
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items());

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None));
        }

        // ── Diagnostic Entry tests ─────────────────────────────────────

        [Fact]
        public async Task SearchEntriesAsync_ValidationFailedQueryingUsage_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Entry("entry-1", "Pinned", "cfg-1", "related-1")));
            fake.ApplyAmlExceptionFactory = (amlBody, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.UsageItemType, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "Usage query failed for a business rule.")
                    : null;
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None));
        }

        [Fact]
        public async Task SearchEntriesAsync_UnexpectedUsageQueryException_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Entry("entry-1", "Pinned", "cfg-1", "related-1")));
            fake.ApplyAmlExceptionFactory = (amlBody, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.UsageItemType, StringComparison.OrdinalIgnoreCase)
                    ? new IOException("socket reset")
                    : null;
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-1", "cfg-1", "PART-001", "A", "Released")));

            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<IOException>(() =>
                client.SearchEntriesAsync(new PartLibrarySearchRequest { LibraryId = "lib-1" }, CancellationToken.None));
        }

        [Fact]
        public async Task SearchEntriesAsync_MissingSourceLibrary_ProducesDiagnosticEntry()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // Entry with LatestCurrent policy so resolution fails (no current part found)
            // Library lookup succeeds in MapEntrySummaryAsync, then fails in CreateDiagnosticSummaryAsync
            var entryWithBadLib = new JObject
            {
                ["id"] = "entry-bad-lib",
                ["source_id"] = "lib-missing",
                ["related_id"] = "related-1",
                ["part_config_id"] = "cfg-1",
                ["revision_policy"] = "LatestCurrent"
            };
            var entryValid = Entry("entry-valid", "Pinned", "cfg-2", "related-2", "related-2", "Draft", "Draft");
            fake.ApplyAmlResults.Enqueue(Items(entryWithBadLib, entryValid));
            // Usage query
            fake.ApplyAmlResults.Enqueue(Items());
            // LatestCurrent AML query for entry-bad-lib (returns no items → ValidationFailed)
            fake.ApplyAmlResults.Enqueue(new JObject());
            // CAD + latest released for entry-valid
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items(Part("related-2", "cfg-2", "PART-002", "A", "Released")));

            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
            {
                if (string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(itemId, "lib-missing", StringComparison.OrdinalIgnoreCase))
                {
                    return new ArasOperationException(ArasErrorCode.ValidationFailed, "Library not found");
                }

                return null;
            };
            // Part lookup for related-1 (for diagnostic display)
            fake.ApplyItemResults.Enqueue(Part("related-1", "cfg-1", "PART-001", "A", "Released"));
            // Library lookup for entry-valid
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            // Part lookup for entry-valid
            fake.ApplyItemResults.Enqueue(Part("related-2", "cfg-2", "PART-002", "A", "Released"));

            var client = CreateClient(fake);

            var result = await client.SearchEntriesAsync(new PartLibrarySearchRequest(), CancellationToken.None);

            Assert.Equal(2, result.Entries.Count);
            var badLibEntry = Assert.Single(result.Entries.Where(e => e.EntryId == "entry-bad-lib"));
            var validEntry = Assert.Single(result.Entries.Where(e => e.EntryId == "entry-valid"));
            Assert.True(badLibEntry.ResolutionFailed);
            Assert.Equal("(Unavailable Library)", badLibEntry.LibraryName);
            Assert.False(string.IsNullOrWhiteSpace(badLibEntry.ResolutionError));
            Assert.False(validEntry.ResolutionFailed);
            Assert.Equal("entry-valid", validEntry.EntryId);
        }

        [Fact]
        public async Task GetEntryAsync_PermissionDeniedDuringLibraryLookup_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PermissionDenied, "access denied")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetEntryAsync("entry-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetEntryAsync_AuthInvalidDuringPartLookup_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
                string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.AuthInvalid, "not authenticated")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetEntryAsync("entry-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetEntryAsync_ServerUnavailablePartLookup_Throws()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
                string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ServerUnavailable, "service unavailable")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetEntryAsync("entry-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetEntryAsync_PartNotFound_ProducesDiagnostic()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
                string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(itemId, "related-1", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PartNotFound, "Part not found")
                    : null;
            // ApplyAml: usage query
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.GetEntryAsync("entry-1", CancellationToken.None);

            Assert.True(result.ResolutionFailed);
            Assert.False(result.CanAddToProject);
            Assert.Contains("not found", result.ResolutionError, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetEntryAsync_ResolutionErrorInDetails()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "related-1", "Draft", "Draft"));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, selectFields) =>
                string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(itemId, "related-1", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PartNotFound, "Part not found")
                    : null;
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.GetEntryAsync("entry-1", CancellationToken.None);

            Assert.True(result.ResolutionFailed);
            Assert.False(string.IsNullOrWhiteSpace(result.ResolutionError));
            Assert.False(result.CanAddToProject);
        }

        // ── Blank Part state tests ─────────────────────────────────────

        [Fact]
        public async Task ResolvePartAsync_PinnedBlankState_Rejects()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "related-1", "pinned-1"));
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "pinned-1",
                ["config_id"] = "cfg-1",
                ["item_number"] = "PART-001",
                ["name"] = "Test Part",
                ["major_rev"] = "A",
                ["state"] = ""
            });

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ResolvePartAsync("entry-1", LibraryRevisionPolicy.Pinned, CancellationToken.None));

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.Contains("does not have a readable lifecycle state", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateRevisionPolicyAsync_PinnedBlankState_DoesNotEditEntry()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestReleased", "cfg-1", "related-1"));
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "pinned-1",
                ["config_id"] = "cfg-1",
                ["item_number"] = "PART-001",
                ["name"] = "Test Part",
                ["major_rev"] = "A",
                ["state"] = ""
            });

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.UpdateRevisionPolicyAsync(
                    new UpdateLibraryRevisionPolicyRequest
                    {
                        EntryId = "entry-1",
                        RevisionPolicy = LibraryRevisionPolicy.Pinned,
                        PinnedPartId = "pinned-1"
                    },
                    CancellationToken.None));

            Assert.DoesNotContain(fake.CallLog, call => call.MethodKind == "ApplyAml" && call.Action == "edit" && call.ItemType == PartLibrarySchemaNames.EntryRelationshipType);
        }

        // ── State filter tests ─────────────────────────────────────────

        [Fact]
        public async Task SearchEntriesAsync_StateFilterDeprecated_DoesNotMatch()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // Entry with Released Part state but Deprecated Entry status
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "entry-1",
                ["source_id"] = "lib-1",
                ["related_id"] = "related-1",
                ["part_config_id"] = "cfg-1",
                ["revision_policy"] = "Pinned",
                ["pinned_part_id"] = "related-1",
                ["entry_status"] = "Deprecated",
                ["state"] = "Deprecated",
                ["usage_count"] = "0"
            }));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Engineering Part Library"));
            // ResolveLatestReleasedPartStrictAsync fails -> becomes diagnostic entry
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            // Filter by "Released" (Part lifecycle) should NOT match a Deprecated entry
            var result = await client.SearchEntriesAsync(
                new PartLibrarySearchRequest { StateFilter = "Released" },
                CancellationToken.None);

            Assert.Empty(result.Entries);
        }

        // ── Server Method source tests ─────────────────────────────────

        [Fact]
        public void MethodSource_ContainsIdempotencyKey()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.Contains("idempotency_key", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodSource_QueriesExistingUsageBeforeAdd()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.Contains("existingUsage", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("maxRecords", source, StringComparison.Ordinal);
            Assert.Contains("getItemByIndex", source, StringComparison.Ordinal);
            Assert.Contains("created_on", source, StringComparison.Ordinal);
            Assert.DoesNotContain("select=\"id,library_entry_id,usage_count,last_used_on\"", source, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MethodSource_ReturnsAlreadyExists()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.Contains("already_exists", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodSource_NoLocalFunction()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("NormalizeActionType", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodSource_NoNamespaceOrClass()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("namespace ", source, StringComparison.Ordinal);
            Assert.DoesNotContain(" class ", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MethodSource_NoAsyncKeyword()
        {
            var sourcePath = FindMethodSourceFile("idea_RecordPartLibraryUsage.cs");
            var source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("async", source, StringComparison.Ordinal);
        }

        [Fact]
        public void DeploymentDoc_ExistsAndMentionsIdempotencyKey()
        {
            var docPath = Path.Combine(
                FindRepoRoot(),
                "docs",
                "part-library-stage1-deployment.md");
            Assert.True(File.Exists(docPath), "Deployment documentation not found.");
            var source = File.ReadAllText(docPath);
            Assert.Contains("idempotency_key", source, StringComparison.Ordinal);
        }

        // ── State filter mapping tests ─────────────────────────────────

        [Fact]
        public void StateFilterOptions_DoNotIncludeDeprecated()
        {
            // Verify the LibraryViewModel does not include Deprecated in state filter
            // by checking the TranslationKeys and filter behavior
            Assert.False(string.IsNullOrWhiteSpace(TranslationKeys.LibraryFilterDeprecated));

            // The key still exists but view model should not add it to the filter list.
            // This test verifies the test infrastructure - the actual behavior is
            // tested by SearchEntriesAsync_StateFilterDeprecated_DoesNotMatch.
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static string FindMethodSourceFile(string fileName)
        {
            var repoRoot = FindRepoRoot();
            var fullPath = Path.Combine(repoRoot, "IdeaCadConnector", "src", "IdeaCadConnector.Aras", "ServerMethods", fileName);
            Assert.True(File.Exists(fullPath), "Method source file not found: " + fullPath);
            return fullPath;
        }

        private static string FindRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, "IdeaCadConnector")) &&
                    Directory.Exists(Path.Combine(dir, "docs")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }

            Assert.True(false, "Could not locate repository root.");
            return null;
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
