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
        public async Task GetCadDetailsAsync_PinnedPolicy_UsesPinnedPartAndReturnsAvailable()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "Pinned", "cfg-1", "part-current", "part-pinned", "Draft", "Draft"));
            fake.ApplyItemResults.Enqueue(Part("part-pinned", "cfg-1", "PIN-001", "A", "Released"));
            fake.ApplyItemResults.Enqueue(Part("part-pinned", "cfg-1", "PIN-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = cadId }));
            fake.ApplyItemResults.Enqueue(Cad(cadId, "PIN-001-ICS", "PIN-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", fileId));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("PIN-001-ICS", details.PrimaryCadNumber);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
            Assert.True(details.HasNative);
        }

        [Fact]
        public async Task GetCadDetailsAsync_RelatedCadWithoutNativeFile_ReturnsNoNativeFile()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = cadId }));
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released"));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("No native file", details.CadStatus);
            Assert.False(details.HasNative);
        }

        [Fact]
        public async Task GetCadDetailsAsync_RelatedIdAsObjectWithId_ReturnsAvailable()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = new JObject { ["id"] = cadId, ["keyed_name"] = "ABC-001-ICS" }
            }));
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", fileId));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
        }

        [Fact]
        public async Task GetCadDetailsAsync_RelatedIdAsNestedItemWithId_ReturnsAvailable()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = new JObject
                {
                    ["Item"] = new JObject { ["id"] = cadId, ["keyed_name"] = "ABC-001-ICS" }
                }
            }));
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", fileId));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
        }

        [Fact]
        public async Task GetCadDetailsAsync_NestedCadWithNativeFile_ReturnsAvailableWithoutCadItemReload()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = new JObject
                {
                    ["Item"] = Cad("cad-1", "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", "file-1")
                }
            }));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ServerUnavailable, "CAD item type not readable")
                    : null;

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal("cad-1", details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal("file-1", details.FileId);
            Assert.DoesNotContain(fake.Calls, call => call.MethodKind == "ApplyItem" && call.ItemType == "CAD");
        }

        [Fact]
        public async Task GetCadDetailsAsync_NestedCadWithoutNativeFile_ReturnsNoNativeFile()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = new JObject
                {
                    ["Item"] = Cad("cad-1", "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released")
                }
            }));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal("cad-1", details.PrimaryCadId);
            Assert.Equal("No native file", details.CadStatus);
            Assert.False(details.HasNative);
        }

        [Fact]
        public async Task GetCadDetailsAsync_RelatedIdMissing_ReturnsDiagnosticStatus()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["id"] = "rel-1" }));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.StartsWith("CAD lookup unavailable:", details.CadStatus, StringComparison.Ordinal);
            Assert.Contains("related_id", details.CadStatus, StringComparison.OrdinalIgnoreCase);
            Assert.Null(details.PrimaryCadId);
        }

        [Fact]
        public async Task GetCadDetailsAsync_PartCadRelationshipUnavailable_TriesCadDocumentsFallback()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = new JObject
                {
                    ["Item"] = Cad("cad-1", "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", "file-1")
                }
            }));
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, "Part CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ValidationFailed, "Part CAD item type not found")
                    : null;

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal("cad-1", details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Contains(fake.Calls, call => call.MethodKind == "ApplyAml" && call.ItemType == "CAD Documents");
        }

        [Fact]
        public async Task GetCadDetailsAsync_NoPartCadRel_FallsBackToExpectedCadAndMarksUnlinked()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(Items(Cad("cad-1", "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "A", "Released", "file-1")));

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal("cad-1", details.PrimaryCadId);
            Assert.Equal("Unlinked CAD found", details.CadStatus);
            Assert.Equal("file-1", details.FileId);
            Assert.True(details.HasNative);
        }

        [Fact]
        public async Task GetCadDetailsAsync_NoPartCadRel_AndNoFallback_ReturnsNoCad()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items());
            fake.ApplyAmlResults.Enqueue(Items());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Null(details.PrimaryCadId);
            Assert.Equal("No CAD", details.CadStatus);
            Assert.False(details.HasNative);
        }

        [Fact]
        public async Task GetCadDetailsAsync_LookupUnavailable_ReturnsLookupUnavailable()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, "Part CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.ServerUnavailable, "Down")
                    : null;

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.StartsWith("CAD lookup unavailable:", details.CadStatus, StringComparison.Ordinal);
            Assert.Contains("Down", details.CadStatus, StringComparison.Ordinal);
            Assert.Null(details.PrimaryCadId);
        }

        [Fact]
        public async Task GetCadDetailsAsync_PermissionDenied_Propagates()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, "Part CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PermissionDenied, "Denied")
                    : null;

            var client = CreateClient(fake);

            var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetCadDetailsAsync("entry-1", CancellationToken.None));

            Assert.Equal(ArasErrorCode.PermissionDenied, ex.ErrorCode);
        }

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

        private static JObject Cad(
            string id,
            string itemNumber,
            string name,
            string classification,
            string authoringTool,
            string generation,
            string state,
            string nativeFile = null)
        {
            return new JObject
            {
                ["id"] = id,
                ["item_number"] = itemNumber,
                ["name"] = name,
                ["classification"] = classification,
                ["authoring_tool"] = authoringTool,
                ["generation"] = generation,
                ["state"] = state,
                ["locked_by_id"] = null,
                ["native_file"] = nativeFile
            };
        }

        // ── IsArasId ─────────────────────────────────────────────────────

        [Fact]
        public void IsArasId_Valid32Hex_ReturnsTrue()
        {
            Assert.True(HttpPartLibraryClient.IsArasId("A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6"));
            Assert.True(HttpPartLibraryClient.IsArasId("117886DD56B674D969D9B8910A891FF3"));
            Assert.True(HttpPartLibraryClient.IsArasId("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6"));
        }

        [Fact]
        public void IsArasId_InvalidLength_ReturnsFalse()
        {
            Assert.False(HttpPartLibraryClient.IsArasId(null));
            Assert.False(HttpPartLibraryClient.IsArasId(""));
            Assert.False(HttpPartLibraryClient.IsArasId("ABC123"));
            Assert.False(HttpPartLibraryClient.IsArasId("A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6!")); // 33 chars
            Assert.False(HttpPartLibraryClient.IsArasId("A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D")); // 31 chars
        }

        [Fact]
        public void IsArasId_NonHexChars_ReturnsFalse()
        {
            Assert.False(HttpPartLibraryClient.IsArasId("Z1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6")); // 'Z' not hex
            Assert.False(HttpPartLibraryClient.IsArasId("G1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6")); // 'G' not hex
        }

        // ── ExtractArasItemId ───────────────────────────────────────────

        [Fact]
        public void ExtractArasItemId_ReturnsRawId_WhenTokenIsString()
        {
            const string expected = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var token = new JValue(expected);
            var result = HttpPartLibraryClient.ExtractArasItemId(token);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractArasItemId_ExtractsCadId_FromConcatenatedLiveString()
        {
            const string cadId = "117886DD56B674D969D9B8910A891FF3";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var concatenated = "IronCADMechanical/Part" + cadId + "IRONCASE_Ver1.0_001.ics" + fileId;
            var token = new JValue(concatenated);
            var result = HttpPartLibraryClient.ExtractArasItemId(token);
            Assert.Equal(cadId, result);
        }

        [Fact]
        public void ExtractArasItemId_NeverReturnsFullConcatenatedString()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var concatenated = "IronCADMechanical/Part" + cadId + "IRONCASE_Ver1.0_001.ics";
            var token = new JValue(concatenated);
            var result = HttpPartLibraryClient.ExtractArasItemId(token);
            Assert.NotEqual(concatenated, result);
            Assert.True(result == null || result.Length == 32);
        }

        [Fact]
        public void ExtractArasItemId_MultipleHexIds_ReturnsFirstAsCadId()
        {
            const string expectedCadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var concatenated = "IronCADMechanical/Part" + expectedCadId + "File" + fileId;
            var token = new JValue(concatenated);
            var result = HttpPartLibraryClient.ExtractArasItemId(token);
            Assert.Equal(expectedCadId, result);
        }

        [Fact]
        public void ExtractArasItemId_ReturnsNull_WhenNoHexIdFound()
        {
            var input = "IronCADMechanical/PartSomeRandomTextNoHexIdHere";
            var token = new JValue(input);
            var result = HttpPartLibraryClient.ExtractArasItemId(token);
            Assert.Null(result);
        }

        [Fact]
        public void ExtractArasItemId_ReturnsNull_ForNullToken()
        {
            var result = HttpPartLibraryClient.ExtractArasItemId(null);
            Assert.Null(result);
        }

        // ── GetPrimaryCadInfoAsync: no ApplyItemAsync with invalid id ───

        [Fact]
        public async Task GetPrimaryCadInfoAsync_DoesNotCallApplyItemWithInvalidCadId()
        {
            var fake = new FakeArasAmlClient();
            // Entry for GetEntryRelationshipAsync
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            // ResolveCurrentPartStrictAsync → ApplyAml for current part
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            // GetPartAsync inside GetPrimaryCadInfoAsync
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // GetPartCadRelationshipResultAsync → row with concatenated related_id
            const string hexCadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string hexFileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var concatId = "IronCADMechanical/Part" + hexCadId + "IRONCASE.ics" + hexFileId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = "rel-1",
                ["related_id"] = concatId
            }));
            // ApplyItemAsync("CAD", hexCadId, "get", ...) → return a valid CAD with native file
            var hexCadIdUpper = hexCadId.ToUpperInvariant();
            fake.ApplyItemResults.Enqueue(Cad(hexCadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", hexFileId));
            // GetLatestReleasedPartAsync → empty
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            // The extracted hex ID should be used, not the concatenated string
            Assert.Equal(hexCadId, details.PrimaryCadId);
            // No ApplyItemAsync("CAD", ...) call should have the concatenated string as ItemId
            var cadCalls = fake.Calls.Where(c =>
                string.Equals(c.ItemType, "CAD", StringComparison.OrdinalIgnoreCase) &&
                c.MethodKind == "ApplyItem");
            Assert.DoesNotContain(cadCalls, c => c.ItemId == concatId);
            Assert.Contains(cadCalls, c => string.Equals(c.ItemId, hexCadId, StringComparison.OrdinalIgnoreCase));
        }

        // ── CreatePrimaryCadInfoFromRelationshipRow ─────────────────────

        [Fact]
        public void CreatePrimaryCadInfoFromRelationshipRow_ExpandedFields_ReturnsAvailable()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var rel = new JObject
            {
                ["id"] = cadId,
                ["item_number"] = "IRONCASE-02-01",
                ["name"] = "IRONCASE_Ver1.0",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "IronCAD",
                ["generation"] = "1",
                ["state"] = "Released",
                ["native_file"] = fileId
            };

            var info = HttpPartLibraryClient.CreatePrimaryCadInfoFromRelationshipRow(rel);

            Assert.NotNull(info);
            Assert.Equal("Available", info.Status);
            Assert.Equal(cadId, info.CadId);
            Assert.Equal("IRONCASE-02-01", info.CadNumber);
            Assert.Equal(fileId, info.FileId);
            Assert.Equal("IRONCASE_Ver1.0", info.FileName);
        }

        [Fact]
        public void CreatePrimaryCadInfoFromRelationshipRow_NativeFileObject_ReturnsFileId()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var rel = new JObject
            {
                ["id"] = cadId,
                ["item_number"] = "IRONCASE-02-01",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "IronCAD",
                ["native_file"] = new JObject
                {
                    ["id"] = fileId,
                    ["name"] = "IRONCASE_Ver1.0_001.ics"
                }
            };

            var info = HttpPartLibraryClient.CreatePrimaryCadInfoFromRelationshipRow(rel);

            Assert.NotNull(info);
            Assert.Equal("Available", info.Status);
            Assert.Equal(fileId, info.FileId);
            Assert.Equal("IRONCASE_Ver1.0_001.ics", info.FileName);
        }

        [Fact]
        public void CreatePrimaryCadInfoFromRelationshipRow_NativeFileString_ReturnsFileId()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var rel = new JObject
            {
                ["id"] = cadId,
                ["item_number"] = "IRONCASE-02-01",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "IronCAD",
                ["native_file"] = fileId
            };

            var info = HttpPartLibraryClient.CreatePrimaryCadInfoFromRelationshipRow(rel);

            Assert.NotNull(info);
            Assert.Equal("Available", info.Status);
            Assert.Equal(fileId, info.FileId);
        }

        [Fact]
        public void CreatePrimaryCadInfoFromRelationshipRow_NoCadFields_ReturnsNull()
        {
            var rel = new JObject
            {
                ["id"] = "rel-1",
                ["source_id"] = "lib-1",
                ["related_id"] = "part-1"
            };

            var info = HttpPartLibraryClient.CreatePrimaryCadInfoFromRelationshipRow(rel);

            Assert.Null(info);
        }

        [Fact]
        public void CreatePrimaryCadInfoFromRelationshipRow_OnlyFileId_ReturnsAvailable()
        {
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var rel = new JObject
            {
                ["item_number"] = "SOME-DOC-01",
                ["authoring_tool"] = "IronCAD",
                ["classification"] = "Mechanical/Part",
                ["native_file"] = fileId
            };

            var info = HttpPartLibraryClient.CreatePrimaryCadInfoFromRelationshipRow(rel);

            Assert.NotNull(info);
            Assert.Equal("Available", info.Status);
            Assert.Null(info.CadId);
            Assert.Equal(fileId, info.FileId);
        }

        // ── ExtractArasItemIdCandidates ─────────────────────────────────

        [Fact]
        public void ExtractArasItemIdCandidates_ReturnsOneRawId()
        {
            const string id = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var token = new JValue(id);
            var result = HttpPartLibraryClient.ExtractArasItemIdCandidates(token);
            Assert.Single(result);
            Assert.Equal(id, result[0]);
        }

        [Fact]
        public void ExtractArasItemIdCandidates_ReturnsAllIds_FromConcatenatedString()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            const string relId = "11111111111111111111111111111111";
            // Use non-hex separators to avoid overlapping hex matches
            var concatenated = "xxx" + relId + "xxx" + cadId + "xxx" + fileId;
            var token = new JValue(concatenated);
            var result = HttpPartLibraryClient.ExtractArasItemIdCandidates(token);
            Assert.Equal(3, result.Count);
            Assert.Contains(relId, result);
            Assert.Contains(cadId, result);
            Assert.Contains(fileId, result);
        }

        [Fact]
        public void ExtractArasItemIdCandidates_DeduplicatesIds()
        {
            const string id = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            // Use a non-hex separator
            var concatenated = id + "xxx" + id;
            var token = new JValue(concatenated);
            var result = HttpPartLibraryClient.ExtractArasItemIdCandidates(token);
            Assert.Single(result);
            Assert.Equal(id, result[0]);
        }

        [Fact]
        public void ExtractArasItemIdCandidates_NeverReturnsNonHexStrings()
        {
            var token = new JValue("cad-1");
            var result = HttpPartLibraryClient.ExtractArasItemIdCandidates(token);
            Assert.Empty(result);

            token = new JValue("IronCADMechanical/PartSomeRandomTextNoHexIdHere");
            result = HttpPartLibraryClient.ExtractArasItemIdCandidates(token);
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractArasItemId_Compatibility_ReturnsFirstCandidateOrNull()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var concatenated = "IronCADMechanical/Part" + cadId + "IRONCASE.ics" + fileId;
            var result = HttpPartLibraryClient.ExtractArasItemId(new JValue(concatenated));
            Assert.Equal(cadId, result);

            var noId = HttpPartLibraryClient.ExtractArasItemId(new JValue("no-hex-here"));
            Assert.Null(noId);
        }

        // ── GetPrimaryCadInfoAsync: multiple candidates ──────────────────

        [Fact]
        public async Task GetPrimaryCadInfoAsync_TriesNextCandidate_WhenFirstReturnsNotFound()
        {
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // Part CAD returns row with concatenated string containing two IDs
            var concat = "SomePrefix" + fileId + "suffix" + cadId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // First candidate (fileId) → not found
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(itemId, fileId, StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found")
                    : null;
            // Second candidate (cadId) → found
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            // GetLatestReleasedPartAsync → empty
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            var cadCalls = fake.Calls.Where(c =>
                c.MethodKind == "ApplyItem" &&
                string.Equals(c.ItemType, "CAD", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(cadCalls, c => string.Equals(c.ItemId, fileId, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(cadCalls, c => string.Equals(c.ItemId, cadId, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_Succeeds_WhenSecondCandidateIsCadId()
        {
            const string relId = "11111111111111111111111111111111";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // Three IDs in concatenated string: relId, cadId, fileId
            var concat = "Prefix" + relId + "Mid" + cadId + "Suffix" + fileId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // relId → not found, fileId → not found, cadId → found
            int callCount = 0;
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
            {
                if (!string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase))
                    return null;
                callCount++;
                if (callCount <= 2) // first two candidates fail
                    return new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found");
                return null;
            };
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_DoesNotStop_AfterFirstNotFoundCandidate()
        {
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            var concat = "IronCAD/Part" + fileId + "File" + cadId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // First candidate (fileId) → not found
            int callIdx = 0;
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
            {
                if (!string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase))
                    return null;
                callIdx++;
                if (callIdx == 1)
                    return new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found");
                return null;
            };
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_FileIdFirst_CadIdSecond_ReturnsCad()
        {
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // fileId appears first in concatenated string, cadId second
            var concat = fileId + cadId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // fileId → returns a File item (not CAD-like), cadId → returns proper CAD
            fake.ApplyItemResults.Enqueue(new JObject
            {
                ["id"] = fileId,
                ["item_number"] = null,
                ["classification"] = null,
                ["authoring_tool"] = null,
                ["generation"] = null,
                ["state"] = null,
                ["locked_by_id"] = null,
                ["native_file"] = null
            });
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_RelIdFirst_CadIdSecond_ReturnsCad()
        {
            const string relId = "11111111111111111111111111111111";
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // relId first, cadId second, fileId third
            var concat = relId + cadId + fileId;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // relId → not found
            int callIdx = 0;
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
            {
                if (!string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase))
                    return null;
                callIdx++;
                if (callIdx == 1)
                    return new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found");
                return null;
            };
            // cadId → found
            fake.ApplyItemResults.Enqueue(Cad(cadId, "ABC-001-ICS", "ABC-001.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_AllCandidatesNotFound_ReturnsDiagnostic()
        {
            const string id1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string id2 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            var concat = id1 + "x" + id2;
            fake.ApplyAmlResults.Enqueue(Items(new JObject { ["related_id"] = concat }));
            // Both candidates not found
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found")
                    : null;
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Contains("tried 2 CAD id candidates; none resolved", details.CadStatus);
            Assert.Null(details.PrimaryCadId);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_AllCandidatesNotFound_FallsBackToCadDocumentNumber()
        {
            const string relId = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string partConfigId = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            const string fileId = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
            const string cadId = "D1D2D3D4D5D6A7B8C9D0E1F2A3B4C5D6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "IRONCASE-03-02", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "IRONCASE-03-02", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = relId + partConfigId + fileId,
                ["item_number"] = "IRONCASE-03-02-ICS",
                ["keyed_name"] = "IRONCASE-03-02-ICS"
            }));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.CadNotFound, "CAD not found")
                    : null;
            fake.ApplyAmlResults.Enqueue(Items(Cad(cadId, "IRONCASE-03-02-ICS", "IRONCASE_Ver1.0_006.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId)));
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.True(
                fake.AnyAmlContains("<item_number>IRONCASE-03-02-ICS</item_number>"),
                string.Join("\n---\n", fake.Calls.Select(c => c.MethodKind + " " + c.ItemType + " " + c.ItemId + "\n" + c.AmlBody)));
            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_ServerMethodResult_WinsBeforeClientRelationshipParsing()
        {
            const string cadId = "D1D2D3D4D5D6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "IRONCASE-02-01", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "IRONCASE-02-01", "A", "Released"));
            fake.ApplyMethodResults.Enqueue(Cad(cadId, "IRONCASE-02-01-ICS", "IRONCASE_Ver1.0_003.ics", "Mechanical/Part", "IronCAD", "1", "Released", fileId));
            fake.ApplyAmlExceptionFactory = (aml, action, itemType, itemId) =>
                string.Equals(itemType, "Part CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.UnexpectedServerError, "Client-side Part CAD lookup should not run")
                    : null;

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
            Assert.Contains(fake.Calls, call =>
                call.MethodKind == "ApplyMethod" &&
                call.MethodName == PartLibrarySchemaNames.GetPrimaryIronCadForPartMethodName);
            Assert.DoesNotContain(fake.Calls, call =>
                call.MethodKind == "ApplyAml" &&
                string.Equals(call.ItemType, "Part CAD", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_AuthFailure_Propagates()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6"
            }));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase)
                    ? new ArasOperationException(ArasErrorCode.PermissionDenied, "Access denied")
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.GetCadDetailsAsync("entry-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_Canceled_Propagates()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["related_id"] = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6"
            }));
            fake.ApplyItemExceptionFactory = (itemType, itemId, action, select) =>
                string.Equals(itemType, "CAD", StringComparison.OrdinalIgnoreCase)
                    ? new OperationCanceledException()
                    : null;

            var client = CreateClient(fake);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                client.GetCadDetailsAsync("entry-1", CancellationToken.None));
        }

        [Fact]
        public async Task GetPrimaryCadInfoAsync_ExpandedRowWithNativeFile_MapsDirectly()
        {
            const string cadId = "A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";
            const string fileId = "F1E2D3C4B5A6F7E8D9C0B1A2F3E4D5C6";
            var fake = new FakeArasAmlClient();
            fake.ApplyItemResults.Enqueue(Entry("entry-1", "LatestCurrent", "cfg-1", "part-1"));
            fake.ApplyAmlResults.Enqueue(Items(Part("part-1", "cfg-1", "ABC-001", "A", "Released")));
            fake.ApplyItemResults.Enqueue(Part("part-1", "cfg-1", "ABC-001", "A", "Released"));
            // Relationship row with expanded CAD fields but no nested CAD (simulates flattened response)
            fake.ApplyAmlResults.Enqueue(Items(new JObject
            {
                ["id"] = cadId,
                ["item_number"] = "IRONCASE-02-01",
                ["name"] = "IRONCASE_Ver1.0",
                ["classification"] = "Mechanical/Part",
                ["authoring_tool"] = "IronCAD",
                ["generation"] = "1",
                ["state"] = "Released",
                ["native_file"] = fileId,
                ["related_id"] = "ignored-concatenated-string"
            }));
            // GetLatestReleasedPartAsync
            fake.ApplyAmlResults.Enqueue(new JObject());

            var client = CreateClient(fake);

            var details = await client.GetCadDetailsAsync("entry-1", CancellationToken.None);

            // Should map directly from expanded row fields without calling ApplyItemAsync("CAD", ...)
            Assert.Equal(cadId, details.PrimaryCadId);
            Assert.Equal("Available", details.CadStatus);
            Assert.Equal(fileId, details.FileId);
            var cadApplyCalls = fake.Calls.Where(c =>
                c.MethodKind == "ApplyItem" &&
                string.Equals(c.ItemType, "CAD", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(cadApplyCalls);
        }
    }
}
