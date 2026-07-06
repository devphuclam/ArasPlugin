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
    public sealed class PartLibraryStage2Tests
    {
        // ── CreateLibraryAsync ───────────────────────────────────────────

        [Fact]
        public async Task CreateLibraryAsync_CreatesSuccessfully()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "lib-1" });

            var client = CreateClient(fake);

            var result = await client.CreateLibraryAsync(
                new CreatePartLibraryRequest { Name = "My Library", Description = "Test", LibraryType = LibraryType.Team },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("lib-1", result.LibraryId);
            Assert.True(fake.AnyAmlContains("My Library"));
            Assert.True(fake.AnyAmlContains("Team"));
        }

        [Fact]
        public async Task CreateLibraryAsync_NullRequest_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.CreateLibraryAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task CreateLibraryAsync_EmptyName_ReturnsError()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);

            var client = CreateClient(fake);

            var result = await client.CreateLibraryAsync(
                new CreatePartLibraryRequest { Name = string.Empty },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("name", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateLibraryAsync_ArasError_Forwards()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "add", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "Duplicate name")
                    : null;

            var client = CreateClient(fake);

            var result = await client.CreateLibraryAsync(
                new CreatePartLibraryRequest { Name = "Duplicate" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        // ── UpdateLibraryAsync ───────────────────────────────────────────

        [Fact]
        public async Task UpdateLibraryAsync_UpdatesSuccessfully()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var result = await client.UpdateLibraryAsync(
                new UpdatePartLibraryRequest { LibraryId = "lib-1", Name = "Updated Name" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("lib-1", result.LibraryId);
            Assert.True(fake.AnyAmlContains("Updated Name"));
        }

        [Fact]
        public async Task UpdateLibraryAsync_NullRequest_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.UpdateLibraryAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateLibraryAsync_EmptyLibraryId_ReturnsError()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);

            var client = CreateClient(fake);

            var result = await client.UpdateLibraryAsync(
                new UpdatePartLibraryRequest { LibraryId = string.Empty },
                CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateLibraryAsync_ArasError_Forwards()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "edit", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PermissionDenied, "Not authorized")
                    : null;

            var client = CreateClient(fake);

            var result = await client.UpdateLibraryAsync(
                new UpdatePartLibraryRequest { LibraryId = "lib-1", Name = "Nope" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.PermissionDenied, result.ErrorCode);
        }

        // ── ArchiveLibraryAsync ──────────────────────────────────────────

        [Fact]
        public async Task ArchiveLibraryAsync_ArchivesSuccessfully()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var result = await client.ArchiveLibraryAsync("lib-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("lib-1", result.LibraryId);
            Assert.True(fake.AnyAmlContains("Archived"));
        }

        [Fact]
        public async Task ArchiveLibraryAsync_EmptyId_ReturnsError()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);

            var client = CreateClient(fake);

            var result = await client.ArchiveLibraryAsync(string.Empty, CancellationToken.None);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task ArchiveLibraryAsync_ArasError_Forwards()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "edit", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PartNotFound, "Library not found")
                    : null;

            var client = CreateClient(fake);

            var result = await client.ArchiveLibraryAsync("lib-missing", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.PartNotFound, result.ErrorCode);
        }

        // ── SearchPartsAsync ─────────────────────────────────────────────

        [Fact]
        public async Task SearchPartsAsync_ReturnsItems()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "ABC-001", "A", "Released", "2"),
                Part("part-2", "cfg-2", "ABC-002", "B", "Preliminary", "1")));

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { Keyword = "ABC", PageSize = 25 },
                CancellationToken.None);

            Assert.Equal(2, result.TotalCount);
            Assert.Equal(2, result.Items.Count);
            Assert.Contains(result.Items, i => i.PartNumber == "ABC-001");
            Assert.Contains(result.Items, i => i.PartNumber == "ABC-002");
        }

        [Fact]
        public async Task SearchPartsAsync_FiltersByLifecycleState()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "REL-001", "A", "Released", "2"),
                Part("part-2", "cfg-2", "DRAFT-001", "B", "Preliminary", "1")));

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { LifecycleState = "Released" },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("Released", amlBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchPartsAsync_FiltersByPartType()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PartType = "Component" },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("Component", amlBody, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SearchPartsAsync_FiltersByCurrentOnly()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { CurrentOnly = true },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("is_current>1<", amlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchPartsAsync_PagingWorks()
        {
            var fake = new FakeArasAmlClient();
            var allParts = Enumerable.Range(1, 50)
                .Select(i => Part("part-" + i, "cfg-" + i, "NUM-" + i.ToString("D3"), "A", "Released", "1"))
                .ToArray();
            // Enqueue twice since we call SearchPartsAsync twice
            fake.ApplyAmlResults.Enqueue(Items(allParts));
            fake.ApplyAmlResults.Enqueue(Items(allParts));

            var client = CreateClient(fake);

            var page1 = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 10, PageNumber = 1 },
                CancellationToken.None);

            Assert.Equal(50, page1.TotalCount);
            Assert.Equal(10, page1.Items.Count);

            var page3 = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 10, PageNumber = 3 },
                CancellationToken.None);

            Assert.Equal(50, page3.TotalCount);
            Assert.Equal(10, page3.Items.Count);
        }

        [Fact]
        public async Task SearchPartsAsync_NullRequest_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.SearchPartsAsync(null, CancellationToken.None));
        }

        // ── GetPartPreviewAsync ──────────────────────────────────────────

        [Fact]
        public async Task GetPartPreviewAsync_ReturnsPartDetails()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released", "2"));
            fake.ApplyAmlResults.Enqueue(Items(
                new JObject { ["related_id"] = "cad-1" }));
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "cad-1",
                ["name"] = "ABC-001.ics",
                ["state"] = "Released",
                ["native_file"] = "file-id"
            });

            var client = CreateClient(fake);

            var preview = await client.GetPartPreviewAsync("part-1", CancellationToken.None);

            Assert.Equal("part-1", preview.PartId);
            Assert.Equal("cfg-1", preview.ConfigId);
            Assert.Equal("A", preview.Revision);
            Assert.Equal("Released", preview.LifecycleState);
            Assert.Equal("2", preview.Generation);
            Assert.True(preview.IsEligibleForReuse);
        }

        [Fact]
        public async Task GetPartPreviewAsync_ObsoletePart_NotEligible()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Part("part-obsolete", "cfg-obs", "OBS-001", "A", "Obsolete", "1"));

            var client = CreateClient(fake);

            var preview = await client.GetPartPreviewAsync("part-obsolete", CancellationToken.None);

            Assert.False(preview.IsEligibleForReuse);
            Assert.Contains("state", preview.IneligibilityReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPartPreviewAsync_NoCad_NotEligible()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Part("part-nocad", "cfg-nocad", "NOCAD-001", "A", "Released", "1"));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var preview = await client.GetPartPreviewAsync("part-nocad", CancellationToken.None);

            Assert.False(preview.IsEligibleForReuse);
            Assert.Contains("CAD", preview.IneligibilityReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetPartPreviewAsync_EmptyPartId_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetPartPreviewAsync(string.Empty, CancellationToken.None));
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        // ── CheckDuplicateEntryAsync ─────────────────────────────────────

        [Fact]
        public async Task CheckDuplicateEntryAsync_NotDuplicate_ReturnsFalse()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-other", "LatestReleased", "cfg-other", "part-other")));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Test Lib"));
            fake.ApplyItemResults.Enqueue(Part("part-other", "cfg-other", "OTHER-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.CheckDuplicateEntryAsync("lib-1", "cfg-target", CancellationToken.None);

            Assert.False(result.IsDuplicate);
            Assert.Null(result.ExistingEntryId);
        }

        [Fact]
        public async Task CheckDuplicateEntryAsync_Duplicate_ReturnsTrue()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-existing", "LatestReleased", "cfg-target", "part-existing")));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Test Lib"));
            fake.ApplyItemResults.Enqueue(Part("part-existing", "cfg-target", "TARGET-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.CheckDuplicateEntryAsync("lib-1", "cfg-target", CancellationToken.None);

            Assert.True(result.IsDuplicate);
            Assert.Equal("entry-existing", result.ExistingEntryId);
        }

        [Fact]
        public async Task CheckDuplicateEntryAsync_DeprecatedEntry_NotConsideredDuplicate()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-deprecated", "LatestReleased", "cfg-target", "part-deprecated", null, "Deprecated", "Deprecated")));
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Test Lib"));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var result = await client.CheckDuplicateEntryAsync("lib-1", "cfg-target", CancellationToken.None);

            Assert.False(result.IsDuplicate);
        }

        [Fact]
        public async Task CheckDuplicateEntryAsync_EmptyLibraryId_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.CheckDuplicateEntryAsync(string.Empty, "cfg-1", CancellationToken.None));
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task CheckDuplicateEntryAsync_EmptyConfigId_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.CheckDuplicateEntryAsync("lib-1", string.Empty, CancellationToken.None));
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        // ── GetLibrariesAsync with visibility filter ─────────────────────

        [Fact]
        public async Task GetLibrariesAsync_ActiveFilter_OnlyActive()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(Library("lib-1", "Active Lib")));
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var libraries = await client.GetLibrariesAsync(LibraryVisibilityFilter.Active, CancellationToken.None);

            var libraryAml = fake.Calls
                .Where(c => c.MethodKind == "ApplyAml")
                .Skip(2) // skip the 2 schema checks
                .FirstOrDefault();
            Assert.NotNull(libraryAml);
            Assert.Contains("Active", libraryAml.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetLibrariesAsync_ArchivedFilter_FiltersByArchived()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                new JObject { ["id"] = "lib-arc", ["name"] = "Archived Lib", ["status"] = "Archived", ["library_type"] = "Team", ["is_public"] = "0" },
                new JObject { ["id"] = "lib-arc2", ["name"] = "Archived Lib 2", ["status"] = "Archived", ["library_type"] = "Team", ["is_public"] = "0" }));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var libraries = await client.GetLibrariesAsync(LibraryVisibilityFilter.Archived, CancellationToken.None);

            var libraryAml = fake.Calls
                .Where(c => c.MethodKind == "ApplyAml")
                .Skip(2)
                .FirstOrDefault();
            Assert.NotNull(libraryAml);
            Assert.Contains("Archived", libraryAml.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetLibrariesAsync_AllFilter_NoStatusFilter()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items(
                Library("lib-active", "Active Lib"),
                new JObject { ["id"] = "lib-archived", ["name"] = "Archived Lib", ["status"] = "Archived", ["library_type"] = "Team", ["is_public"] = "0" }));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var libraries = await client.GetLibrariesAsync(LibraryVisibilityFilter.All, CancellationToken.None);

            var libraryAml = fake.Calls
                .Where(c => c.MethodKind == "ApplyAml")
                .Skip(2)
                .FirstOrDefault();
            Assert.NotNull(libraryAml);
            Assert.DoesNotContain("<status>", libraryAml.AmlBody, StringComparison.Ordinal);
        }

        // ── Helpers ────────────────────────────────────────────────────

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
                ["Items"] = new JArray(items ?? new JObject[0])
            };
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
    }
}
