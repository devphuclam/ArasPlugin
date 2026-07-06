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
            // FindLibraryByNameAsync returns no duplicate
            fake.ApplyAmlResults.Enqueue(Items());
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
        public async Task CreateLibraryAsync_DuplicateName_ReturnsError()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // FindLibraryByNameAsync returns a matching library
            fake.ApplyAmlResults.Enqueue(Items(Library("lib-exists", "Existing")));

            var client = CreateClient(fake);

            var result = await client.CreateLibraryAsync(
                new CreatePartLibraryRequest { Name = "Existing" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateLibraryAsync_ArasError_Forwards()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // FindLibraryByNameAsync returns no duplicate
            fake.ApplyAmlResults.Enqueue(Items());
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

        [Fact]
        public async Task CreateLibraryAsync_ArasError_PropagatesAuth()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "add", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.AuthInvalid, "Not authenticated")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.CreateLibraryAsync(
                    new CreatePartLibraryRequest { Name = "Test" },
                    CancellationToken.None));
        }

        // ── UpdateLibraryAsync ───────────────────────────────────────────

        [Fact]
        public async Task UpdateLibraryAsync_UpdatesSuccessfully()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // FindLibraryByNameAsync returns no duplicate (excludes self)
            fake.ApplyAmlResults.Enqueue(Items());
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
        public async Task UpdateLibraryAsync_DuplicateName_ReturnsError()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // FindLibraryByNameAsync returns a different library with same name
            fake.ApplyAmlResults.Enqueue(Items(Library("lib-other", "Taken Name")));

            var client = CreateClient(fake);

            var result = await client.UpdateLibraryAsync(
                new UpdatePartLibraryRequest { LibraryId = "lib-1", Name = "Taken Name" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateLibraryAsync_ArasError_Forwards()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "edit", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "Name too long")
                    : null;

            var client = CreateClient(fake);

            var result = await client.UpdateLibraryAsync(
                new UpdatePartLibraryRequest { LibraryId = "lib-1", Name = "Nope" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ArasErrorCode.ValidationFailed, result.ErrorCode);
        }

        [Fact]
        public async Task UpdateLibraryAsync_ArasError_PropagatesAuth()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "edit", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.AuthInvalid, "Not authenticated")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.UpdateLibraryAsync(
                    new UpdatePartLibraryRequest { LibraryId = "lib-1", Name = "Nope" },
                    CancellationToken.None));
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

        [Fact]
        public async Task ArchiveLibraryAsync_ServerUnavailable_Propagates()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, PartLibrarySchemaNames.LibraryItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(action, "edit", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ServerUnavailable, "Server down")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.ArchiveLibraryAsync("lib-1", CancellationToken.None));
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
            // Server-side paging: send only 10 items per page
            var page1Parts = Enumerable.Range(1, 10)
                .Select(i => Part("part-" + i, "cfg-" + i, "NUM-" + i.ToString("D3"), "A", "Released", "1"))
                .ToArray();
            var page3Parts = Enumerable.Range(21, 10)
                .Select(i => Part("part-" + i, "cfg-" + i, "NUM-" + i.ToString("D3"), "A", "Released", "1"))
                .ToArray();
            fake.ApplyAmlResults.Enqueue(Items(page1Parts));
            fake.ApplyAmlResults.Enqueue(Items(page3Parts));

            var client = CreateClient(fake);

            var page1 = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 10, PageNumber = 1 },
                CancellationToken.None);

            Assert.Equal(10, page1.TotalCount);
            Assert.Equal(10, page1.Items.Count);

            var page3 = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 10, PageNumber = 3 },
                CancellationToken.None);

            Assert.Equal(10, page3.TotalCount);
            Assert.Equal(10, page3.Items.Count);
        }

        [Fact]
        public async Task SearchPartsAsync_NullRequest_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.SearchPartsAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task SearchPartsAsync_Keyword_UsesOr()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(Part("p1", "c1", "A", "A", "Released")));

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { Keyword = "ABC" },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("<OR>", amlBody, StringComparison.Ordinal);
            Assert.Contains("item_number condition=\"like\"", amlBody, StringComparison.Ordinal);
            Assert.Contains("name condition=\"like\"", amlBody, StringComparison.Ordinal);
            Assert.Contains("ABC%", amlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchPartsAsync_SendsServerSidePaging()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(Part("p1", "c1", "A", "A", "Released")));

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 50, PageNumber = 2 },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("pagesize=\"50\"", amlBody, StringComparison.Ordinal);
            Assert.Contains("page=\"2\"", amlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchPartsAsync_CapsPageSize()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlResults.Enqueue(Items(Part("p1", "c1", "A", "A", "Released")));

            var client = CreateClient(fake);

            var result = await client.SearchPartsAsync(
                new PartPickerSearchRequest { PageSize = 500 },
                CancellationToken.None);

            var amlBody = fake.Calls.First(c => c.MethodKind == "ApplyAml").AmlBody;
            Assert.Contains("pagesize=\"100\"", amlBody, StringComparison.Ordinal);
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

        // ── AddPartAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task AddPartAsync_NullRequest_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.AddPartAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task AddPartAsync_EmptyLibraryId_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.AddPartAsync(
                    new AddPartToLibraryRequest { LibraryId = string.Empty, PartId = "part-1" },
                    CancellationToken.None));
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task AddPartAsync_EmptyPartId_Throws()
        {
            var client = CreateClient(new FakeArasAmlClient());
            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.AddPartAsync(
                    new AddPartToLibraryRequest { LibraryId = "lib-1", PartId = string.Empty },
                    CancellationToken.None));
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public async Task AddPartAsync_ArchivedLibrary_Rejects()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // GetLibraryAsync (ApplyItem) returns archived library
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "lib-arc",
                ["name"] = "Archived",
                ["status"] = "Archived"
            });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest { LibraryId = "lib-arc", PartId = "part-1" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("archived", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddPartAsync_PartWithoutConfigId_Rejects()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // GetLibraryAsync returns active library
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            // GetPartAsync returns part without config_id
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = "part-1",
                ["item_number"] = "ABC-001"
                // no config_id
            });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest { LibraryId = "lib-1", PartId = "part-1" },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("config_id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddPartAsync_AddsWithLatestCurrent()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            // GetLibraryAsync → active library
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            // GetPartAsync → part with config_id, major_rev
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // ResolveCurrentPartStrictAsync → ApplyAml
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            // FindDuplicateEntryIdD02Async → direct D-02 AML result (empty)
            fake.ApplyAmlResults.Enqueue(Items());
            // TryAddPartViaServerMethodAsync → ApplyMethod (returns empty JObject → no method)
            fake.ApplyMethodResults.Enqueue(new JObject());
            // Direct AML add → returns entry id
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "entry-1" });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest
                {
                    LibraryId = "lib-1",
                    PartId = "part-1",
                    RevisionPolicy = LibraryRevisionPolicy.LatestCurrent,
                    Note = "Test"
                },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("entry-1", result.EntryId);
            Assert.False(result.AlreadyExists);
        }

        [Fact]
        public async Task AddPartAsync_AddsWithLatestReleased()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // ResolveLatestReleasedPartStrictAsync
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            // Duplicate check: empty D-02 AML result
            fake.ApplyAmlResults.Enqueue(Items());
            // Server method → none
            fake.ApplyMethodResults.Enqueue(new JObject());
            // Direct AML add
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "entry-1" });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest
                {
                    LibraryId = "lib-1",
                    PartId = "part-1",
                    RevisionPolicy = LibraryRevisionPolicy.LatestReleased
                },
                CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task AddPartAsync_DuplicateEntry_ReturnsAlreadyExists()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // LatestCurrent pre-resolve
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            // FindDuplicateEntryIdD02Async → LoadEntrySummariesAsync:
            //   Entries AML returns entry with matching config_id
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-existing", "LatestCurrent", "cfg-1", "part-existing")));
            //   Usage counts
            fake.ApplyAmlResults.Enqueue(Items());
            //   MapEntrySummaryAsync for the entry:
            //     TryGetLibraryForEntryAsync → GetLibraryAsync → ApplyItem
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Test Lib"));
            //     ResolveCurrentPartStrictAsync → ApplyAml
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-existing", "cfg-1", "ABC-001", "A", "Released")));
            //     GetPrimaryCadInfoAsync → ApplyAml (empty Part CAD)
            fake.ApplyAmlResults.Enqueue(new JObject());
            //     GetLatestReleasedPartAsync → ApplyAml (empty)
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest
                {
                    LibraryId = "lib-1",
                    PartId = "part-1",
                    RevisionPolicy = LibraryRevisionPolicy.LatestCurrent
                },
                CancellationToken.None);

            // Should return AlreadyExists without making AML add call
            Assert.True(result.Success);
            Assert.True(result.AlreadyExists);
            Assert.Equal("entry-existing", result.EntryId);
        }

        [Fact]
        public async Task AddPartAsync_DeprecatedEntry_NotDuplicate()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // LatestCurrent pre-resolve
            fake.ApplyAmlResults.Enqueue(Items(
                Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            // Entries have a deprecated entry with matching config_id
            fake.ApplyAmlResults.Enqueue(Items(
                Entry("entry-dep", "LatestCurrent", "cfg-1", "part-dep", null, "Deprecated", "Deprecated")));
            // Duplicate check path now uses direct D-02 AML only, so no usage-count or summary expansion.

            // No active duplicate found; proceed to add
            fake.ApplyMethodResults.Enqueue(new JObject());
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "entry-new" });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest
                {
                    LibraryId = "lib-1",
                    PartId = "part-1",
                    RevisionPolicy = LibraryRevisionPolicy.LatestCurrent
                },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("entry-new", result.EntryId);
            Assert.False(result.AlreadyExists);
        }

        [Fact]
        public async Task AddPartAsync_AddsWithPinnedPart()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            // GetPartAsync for request.PartId (Pinned policy fetches it again)
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // Request.RevisionPolicy = Pinned, so needs another GetPartAsync (line 251)
            // Actually the Pinned pre-resolve calls GetPartAsync again
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // Duplicate check: empty D-02 AML result
            fake.ApplyAmlResults.Enqueue(Items());
            // Server method → none
            fake.ApplyMethodResults.Enqueue(new JObject());
            // Direct AML add → should include pinned_part_id and pinned_revision
            fake.ApplyAmlResults.Enqueue(new JObject { ["id"] = "entry-pinned" });

            var client = CreateClient(fake);

            var result = await client.AddPartAsync(
                new AddPartToLibraryRequest
                {
                    LibraryId = "lib-1",
                    PartId = "part-1",
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    Note = "Pinned entry"
                },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("entry-pinned", result.EntryId);

            var addAml = fake.Calls
                .Where(c => c.MethodKind == "ApplyAml")
                .LastOrDefault();
            Assert.NotNull(addAml);
            Assert.Contains("pinned_part_id", addAml.AmlBody, StringComparison.Ordinal);
            Assert.Contains("pinned_revision", addAml.AmlBody, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AddPartAsync_AuthError_Propagates()
        {
            var fake = new FakeArasAmlClient();
            EnqueueSchema(fake);
            fake.ApplyItemResults.Enqueue(Library("lib-1", "Active"));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.AuthInvalid, "Not authenticated")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.AddPartAsync(
                    new AddPartToLibraryRequest { LibraryId = "lib-1", PartId = "part-1" },
                    CancellationToken.None));
        }

        // ── Helpers ──────────────────────────────────────────────────────

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
