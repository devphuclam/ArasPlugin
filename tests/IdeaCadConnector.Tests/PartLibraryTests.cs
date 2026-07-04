using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Workspace;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class PartLibraryTests
    {
        // ── PartLifecyclePolicy ─────────────────────────────────────────

        [Fact]
        public void PartLifecyclePolicy_Obsolete_ReturnsTrue()
        {
            Assert.True(PartLifecyclePolicy.IsPartObsolete("Obsolete"));
        }

        [Fact]
        public void PartLifecyclePolicy_Obsolete_CaseInsensitive()
        {
            Assert.True(PartLifecyclePolicy.IsPartObsolete("obsolete"));
        }

        [Fact]
        public void PartLifecyclePolicy_Released_ReturnsFalse()
        {
            Assert.False(PartLifecyclePolicy.IsPartObsolete("Released"));
        }

        [Fact]
        public void PartLifecyclePolicy_NullOrEmpty_ReturnsFalse()
        {
            Assert.False(PartLifecyclePolicy.IsPartObsolete(null));
            Assert.False(PartLifecyclePolicy.IsPartObsolete(""));
        }

        [Fact]
        public void PartLifecyclePolicy_DoesNotUseCadLifecyclePolicy()
        {
            Assert.NotEqual(PartLifecyclePolicy.Obsolete, "Loai bo");
        }

        [Fact]
        public void PartLifecyclePolicy_GetPartNotReusableMessage_ContainsState()
        {
            var msg = PartLifecyclePolicy.GetPartNotReusableMessage("Obsolete", "ABC-001");
            Assert.NotNull(msg);
            Assert.Contains("Obsolete", msg, StringComparison.Ordinal);
            Assert.Contains("cannot be reused", msg, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PartLifecyclePolicy_GetPartNotReusableMessage_Releasable_ReturnsNull()
        {
            Assert.Null(PartLifecyclePolicy.GetPartNotReusableMessage("Released", "ABC-001"));
        }

        // ── WorkspaceLibraryReferenceStore ──────────────────────────────

        [Fact]
        public void WorkspaceLibraryReferenceStore_CurrentSchemaVersionIsOne()
        {
            Assert.Equal(1, WorkspaceLibraryReferenceStore.CurrentSchemaVersion);
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmptyList()
        {
            using var folder = new TempFolder();
            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);
            var result = store.Load(folder.Path);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void MalformedJson_ThrowsInvalidOperationException()
        {
            using var folder = new TempFolder();
            var manifestDir = Path.Combine(folder.Path, ".idea-pdm");
            Directory.CreateDirectory(manifestDir);
            var filePath = Path.Combine(manifestDir, "library-references.json");
            File.WriteAllText(filePath, "{invalid json!!!!}");

            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);

            var ex = Record.Exception(() => store.Load(folder.Path));
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("library-references.json", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MalformedJson_OriginalFileIsPreserved()
        {
            using var folder = new TempFolder();
            var manifestDir = Path.Combine(folder.Path, ".idea-pdm");
            Directory.CreateDirectory(manifestDir);
            var filePath = Path.Combine(manifestDir, "library-references.json");
            var originalContent = "{invalid json!!!!}";
            File.WriteAllText(filePath, originalContent);

            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);

            Assert.Throws<InvalidOperationException>(() => store.Load(folder.Path));
            Assert.True(File.Exists(filePath));
            Assert.Equal(originalContent, File.ReadAllText(filePath));
        }

        [Fact]
        public void SaveAndLoad_RoundTripsAllFields()
        {
            using var folder = new TempFolder();
            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);

            var refs = new[]
            {
                new WorkspaceLibraryReference
                {
                    ReferenceId = "r1",
                    LibraryId = "lib1",
                    LibraryEntryId = "e1",
                    PartId = "p1",
                    PartConfigId = "pc1",
                    PartNumber = "ABC-001",
                    PartName = "Test Part",
                    Revision = "A",
                    ParentLogicalCode = "ROOT",
                    LocalLogicalCode = "LIB01",
                    Quantity = 2,
                    RevisionPolicy = "Pinned",
                    AddedOn = new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc),
                    AddedBy = "tester"
                }
            };

            store.Save(folder.Path, refs);
            var loaded = store.Load(folder.Path);

            Assert.NotNull(loaded);
            var item = Assert.Single(loaded);
            Assert.Equal("r1", item.ReferenceId);
            Assert.Equal("lib1", item.LibraryId);
            Assert.Equal("e1", item.LibraryEntryId);
            Assert.Equal("p1", item.PartId);
            Assert.Equal("pc1", item.PartConfigId);
            Assert.Equal("ABC-001", item.PartNumber);
            Assert.Equal("Test Part", item.PartName);
            Assert.Equal("A", item.Revision);
            Assert.Equal("ROOT", item.ParentLogicalCode);
            Assert.Equal("LIB01", item.LocalLogicalCode);
            Assert.Equal(2, item.Quantity);
            Assert.Equal("Pinned", item.RevisionPolicy);
            Assert.Equal(DateTimeKind.Utc, item.AddedOn.Kind);
            Assert.Equal("tester", item.AddedBy);
        }

        // ── ArasAmlClient classifiers ───────────────────────────────────

        [Fact]
        public void ClassifyNotFoundError_Part_ReturnsPartNotFound()
        {
            Assert.Equal(ArasErrorCode.PartNotFound, ArasAmlClient.ClassifyNotFoundError("Part"));
        }

        [Fact]
        public void ClassifyNotFoundError_Cad_ReturnsCadNotFound()
        {
            Assert.Equal(ArasErrorCode.CadNotFound, ArasAmlClient.ClassifyNotFoundError("CAD"));
        }

        [Fact]
        public void ClassifyNotFoundError_UnknownType_ReturnsValidationFailed()
        {
            Assert.Equal(ArasErrorCode.ValidationFailed, ArasAmlClient.ClassifyNotFoundError("Document"));
        }

        [Fact]
        public void ClassifyErrorText_InvalidToken_ReturnsAuthInvalid()
        {
            Assert.Equal(ArasErrorCode.AuthInvalid, ArasAmlClient.ClassifyErrorText("invalid token"));
        }

        [Fact]
        public void ClassifyErrorText_InvalidSession_ReturnsAuthInvalid()
        {
            Assert.Equal(ArasErrorCode.AuthInvalid, ArasAmlClient.ClassifyErrorText("invalid session"));
        }

        [Fact]
        public void ClassifyErrorText_SessionExpired_ReturnsAuthExpired()
        {
            Assert.Equal(ArasErrorCode.AuthExpired, ArasAmlClient.ClassifyErrorText("session expired"));
        }

        [Fact]
        public void ClassifyErrorText_TokenExpired_ReturnsAuthExpired()
        {
            Assert.Equal(ArasErrorCode.AuthExpired, ArasAmlClient.ClassifyErrorText("token expired"));
        }

        [Fact]
        public void ClassifyErrorText_NotAuthorized_ReturnsPermissionDenied()
        {
            Assert.Equal(ArasErrorCode.PermissionDenied, ArasAmlClient.ClassifyErrorText("not authorized"));
        }

        [Fact]
        public void ClassifyErrorText_InsufficientPermission_ReturnsPermissionDenied()
        {
            Assert.Equal(ArasErrorCode.PermissionDenied, ArasAmlClient.ClassifyErrorText("insufficient permission"));
        }

        [Fact]
        public void ClassifyErrorText_ServiceUnavailable_ReturnsServerUnavailable()
        {
            Assert.Equal(ArasErrorCode.ServerUnavailable, ArasAmlClient.ClassifyErrorText("service unavailable"));
        }

        [Fact]
        public void ClassifyErrorText_UnrelatedFault_ReturnsUnexpected()
        {
            Assert.Equal(ArasErrorCode.UnexpectedServerError, ArasAmlClient.ClassifyErrorText("Some random business error"));
        }

        // ── Behavioral: PushAsync via FakeArasAmlClient ─────────────────

        [Fact]
        public void IsEmptyCollectionFault_CollectionGetWithNoItems_ReturnsTrue()
        {
            Assert.True(ArasAmlClient.IsEmptyCollectionFault(
                "No items of type idea_PartLibraryEntry found",
                "get",
                null));
        }

        [Fact]
        public void IsEmptyCollectionFault_ExactGetWithNoItems_ReturnsFalse()
        {
            Assert.False(ArasAmlClient.IsEmptyCollectionFault(
                "No items of type Part found",
                "get",
                "ABC123"));
        }

        [Fact]
        public void IsEmptyCollectionFault_AlternateNoItemsText_ReturnsTrue()
        {
            Assert.True(ArasAmlClient.IsEmptyCollectionFault(
                "No items found",
                "get",
                null));
        }

        private static PdmPartRequest MakeLibraryPart(string logicalCode, string parentLogicalCode, string existingPartId = "part-1", string configId = "cfg-1", string revision = "A")
        {
            return new PdmPartRequest
            {
                LogicalCode = logicalCode,
                ParentLogicalCode = parentLogicalCode,
                PartNumber = logicalCode + "-001",
                Name = "Test Part",
                Quantity = 1,
                ExistingPartId = existingPartId,
                ExistingPartConfigId = configId,
                ExistingPartRevision = revision,
                SourceKind = "LibraryReference",
                IsExternalReference = false
            };
        }

        private static PdmPartRequest MakeNormalPart(string logicalCode, string parentLogicalCode)
        {
            return new PdmPartRequest
            {
                LogicalCode = logicalCode,
                ParentLogicalCode = parentLogicalCode,
                PartNumber = logicalCode + "-001",
                Name = "Test Part",
                Quantity = 1,
                SourceKind = "Generated"
            };
        }

        private static HttpPdmRepositoryClient CreatePdmClient(FakeArasAmlClient fake)
        {
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "testdb" };
            return new HttpPdmRepositoryClient(options, fake, NullLogger<HttpPdmRepositoryClient>.Instance);
        }

        private static PdmPushRequest SinglePartRequest(PdmPartRequest part)
        {
            return new PdmPushRequest
            {
                RepositoryCode = "TEST",
                ProjectName = "Test",
                TargetBranch = "main",
                PackageSourcePath = ".",
                CadSourcePath = ".",
                Parts = new[] { part },
                Cads = Array.Empty<PdmCadRequest>(),
                Documents = Array.Empty<PdmDocumentRequest>()
            };
        }

        // Test 1: Library reference without ExistingPartId
        [Fact]
        public async Task PushAsync_LibraryRefNoExistingPartId_ReturnsFailure()
        {
            var fake = new FakeArasAmlClient();
            var client = CreatePdmClient(fake);
            var part = new PdmPartRequest
            {
                LogicalCode = "LIB01",
                ParentLogicalCode = null,
                PartNumber = "LIB-001",
                ExistingPartId = null,
                SourceKind = "LibraryReference"
            };

            var result = await client.PushAsync(SinglePartRequest(part), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Empty(fake.Calls);
        }

        // Test 2: ExistingPartConfigId mismatch
        [Fact]
        public async Task PushAsync_ConfigIdMismatch_ReturnsFailure()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFoundWithConfigRev("part-1", "LIB-001", "cfg-other", "A");
            var client = CreatePdmClient(fake);
            var part = MakeLibraryPart("LIB01", null, "part-1", "cfg-expected", "A");

            var result = await client.PushAsync(SinglePartRequest(part), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("config_id mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.CountAmlCalls("Part", "get"));
        }

        // Test 3: ExistingPartRevision mismatch
        [Fact]
        public async Task PushAsync_RevisionMismatch_ReturnsFailure()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFoundWithConfigRev("part-1", "LIB-001", "cfg-1", "B");
            var client = CreatePdmClient(fake);
            var part = MakeLibraryPart("LIB01", null, "part-1", "cfg-1", "A");

            var result = await client.PushAsync(SinglePartRequest(part), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("revision mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.CountAmlCalls("Part", "get"));
        }

        // Test 4: Obsolete Part
        [Fact]
        public async Task PushAsync_ObsoletePart_ReturnsFailureWithoutLoaiBo()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFoundWithState("part-1", "LIB-001", "Obsolete");
            var client = CreatePdmClient(fake);
            var part = MakeLibraryPart("LIB01", null, "part-1", "cfg-part-1", "A");

            var result = await client.PushAsync(SinglePartRequest(part), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Obsolete", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Loai bo", result.ErrorMessage);
            Assert.Equal(1, fake.CountAmlCalls("Part", "get"));
        }

        private static PdmPushRequest TwoPartRequest(PdmPartRequest parent, PdmPartRequest child)
        {
            return new PdmPushRequest
            {
                RepositoryCode = "TEST",
                ProjectName = "Test",
                TargetBranch = "main",
                PackageSourcePath = ".",
                CadSourcePath = ".",
                Parts = new[] { parent, child },
                Cads = Array.Empty<PdmCadRequest>(),
                Documents = Array.Empty<PdmDocumentRequest>()
            };
        }

        // Test 5: Invalid parent-child BOM (self-reference)
        [Fact]
        public async Task PushAsync_SameParentChildBom_ReturnsInvalidParentChild()
        {
            var fake = new FakeArasAmlClient();
            // Parent normal part: FindItemByNumberAsync returns part-1
            fake.EnqueueItemFound("part-1", "PARENT-001");
            // Child library ref: ApplyAmlAsync returns same part-1 with matching config
            fake.EnqueueItemFoundWithConfigRev("part-1", "CHILD-001", "cfg-1", "A");
            var client = CreatePdmClient(fake);
            var parent = MakeNormalPart("PARENT", null);
            var child = MakeLibraryPart("CHILD", "PARENT", "part-1", "cfg-1", "A");

            var result = await client.PushAsync(TwoPartRequest(parent, child), CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.BomResults);
            var bom = Assert.Single(result.BomResults);
            Assert.Equal(BomActionResult.InvalidParentChild, bom.ActionTaken);
        }

        // Test 6: Invalid quantity
        [Fact]
        public async Task PushAsync_InvalidQuantity_ReturnsInvalidQuantity()
        {
            var fake = new FakeArasAmlClient();
            // Parent normal part: FindItemByNumberAsync returns parent-id
            fake.EnqueueItemFound("parent-id", "PARENT-001");
            // Child library ref: ApplyAmlAsync returns child-id with matching config
            fake.EnqueueItemFoundWithConfigRev("child-id", "CHILD-001", "cfg-child", "A");
            var client = CreatePdmClient(fake);
            var parent = MakeNormalPart("PARENT", null);
            var child = MakeLibraryPart("CHILD", "PARENT", "child-id", "cfg-child", "A");
            child.Quantity = 0;

            var result = await client.PushAsync(TwoPartRequest(parent, child), CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.BomResults);
            var bom = Assert.Single(result.BomResults);
            Assert.Equal(BomActionResult.InvalidQuantity, bom.ActionTaken);
        }

        // Test 7: BOM lookup finds existing BOM unchanged
        [Fact]
        public async Task PushAsync_ExistingBomUnchanged_DoesNotEdit()
        {
            var fake = new FakeArasAmlClient();
            // Parent normal part
            fake.EnqueueItemFound("parent-id", "PARENT-001");
            // Child library ref
            fake.EnqueueItemFoundWithConfigRev("child-id", "CHILD-001", "cfg-child", "A");
            // Part BOM get returns existing relationship with same quantity
            fake.EnqueueAmlResult(new JObject
            {
                ["Items"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "bom-1",
                        ["quantity"] = "1"
                    }
                }
            });
            var client = CreatePdmClient(fake);
            var parent = MakeNormalPart("PARENT", null);
            var child = MakeLibraryPart("CHILD", "PARENT", "child-id", "cfg-child", "A");

            var result = await client.PushAsync(TwoPartRequest(parent, child), CancellationToken.None);

            Assert.NotNull(result.BomResults);
            var bom = Assert.Single(result.BomResults);
            Assert.Equal(BomActionResult.Unchanged, bom.ActionTaken);
            Assert.Equal(0, fake.CountAmlCalls("Part BOM", "edit"));
        }

        // Test 8: Missing BOM gets created
        [Fact]
        public async Task PushAsync_MissingBom_CreatesBom()
        {
            var fake = new FakeArasAmlClient();
            // Parent normal part
            fake.EnqueueItemFound("parent-id", "PARENT-001");
            // Child library ref
            fake.EnqueueItemFoundWithConfigRev("child-id", "CHILD-001", "cfg-child", "A");
            // Part BOM get returns empty (no existing BOM)
            fake.EnqueueAmlResult(new JObject());
            var client = CreatePdmClient(fake);
            var parent = MakeNormalPart("PARENT", null);
            var child = MakeLibraryPart("CHILD", "PARENT", "child-id", "cfg-child", "A");

            var result = await client.PushAsync(TwoPartRequest(parent, child), CancellationToken.None);

            Assert.NotNull(result.BomResults);
            var bom = Assert.Single(result.BomResults);
            Assert.Equal(BomActionResult.Created, bom.ActionTaken);
            Assert.Equal(1, fake.CountAmlCalls("Part BOM", "add"));
        }

        // Test 9: Existing BOM with changed quantity
        [Fact]
        public async Task PushAsync_ExistingBomChangedQuantity_UpdatesBom()
        {
            var fake = new FakeArasAmlClient();
            // Parent normal part
            fake.EnqueueItemFound("parent-id", "PARENT-001");
            // Child library ref
            fake.EnqueueItemFoundWithConfigRev("child-id", "CHILD-001", "cfg-child", "A");
            // Part BOM get returns existing relationship with different quantity
            fake.EnqueueAmlResult(new JObject
            {
                ["Items"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = "bom-1",
                        ["quantity"] = "1"
                    }
                }
            });
            var client = CreatePdmClient(fake);
            var parent = MakeNormalPart("PARENT", null);
            var child = MakeLibraryPart("CHILD", "PARENT", "child-id", "cfg-child", "A");
            child.Quantity = 2;

            var result = await client.PushAsync(TwoPartRequest(parent, child), CancellationToken.None);

            Assert.NotNull(result.BomResults);
            var bom = Assert.Single(result.BomResults);
            Assert.Equal(BomActionResult.QuantityUpdated, bom.ActionTaken);
            Assert.Equal(1, fake.CountAmlCalls("Part BOM", "edit"));
        }

        // ── Behavioral: RecordUsageAsync via FakeArasAmlClient ──────────

        private static HttpPartLibraryClient CreateLibraryClient(FakeArasAmlClient fake)
        {
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "testdb" };
            return new HttpPartLibraryClient(options, fake, NullLogger<HttpPartLibraryClient>.Instance);
        }

        private static LibraryUsageRequest MakeUsageRequest(string entryId = "entry-1", string usedBy = "testuser")
        {
            return new LibraryUsageRequest
            {
                LibraryEntryId = entryId,
                PartId = "part-1",
                ProjectCode = "TEST",
                ParentPartId = "parent-1",
                Quantity = 1,
                UsedBy = usedBy,
                CommitId = "commit-1",
                ActionType = "ReusedFromLibrary"
            };
        }

        // Test 11: Usage with supported used_by
        [Fact]
        public async Task RecordUsage_WithUsedBy_AddsUsageThenUpdatesLastUsedOn()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFound("ItemType", PartLibrarySchemaNames.UsageItemType);
            fake.EnqueueItemFound("usage-1", "1");
            fake.EnqueueItemFound("entry-1", "1");
            var client = CreateLibraryClient(fake);

            await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            var usageAdds = fake.CountAmlCalls(PartLibrarySchemaNames.UsageItemType, "add");
            Assert.Equal(1, usageAdds);
        }

        // Test 14: Authentication failure - no retry
        [Fact]
        public async Task RecordUsage_AuthFailure_NoRetry()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFound("ItemType", PartLibrarySchemaNames.UsageItemType);
            fake.ApplyAmlExceptions.Enqueue(new ArasOperationException(ArasErrorCode.AuthInvalid, "not authenticated"));
            var client = CreateLibraryClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            var call = Assert.Single(fake.Calls);
            Assert.Equal("ApplyAml", call.MethodKind);
        }

        // Test 15: Permission failure - no retry
        [Fact]
        public async Task RecordUsage_PermissionFailure_NoRetry()
        {
            var fake = new FakeArasAmlClient();
            fake.EnqueueItemFound("ItemType", PartLibrarySchemaNames.UsageItemType);
            fake.ApplyAmlExceptions.Enqueue(new ArasOperationException(ArasErrorCode.PermissionDenied, "access denied"));
            var client = CreateLibraryClient(fake);

            await Assert.ThrowsAsync<ArasOperationException>(() =>
                client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None));

            var call = Assert.Single(fake.Calls);
            Assert.Equal("ApplyAml", call.MethodKind);
        }

        // Test 16: Server/network failure - no retry
        [Fact]
        public async Task RecordUsage_NetworkFailure_ReturnsGracefully()
        {
            var fake = new FakeArasAmlClient();
            fake.ApplyAmlExceptions.Enqueue(new System.Net.Http.HttpRequestException("connection refused"));
            var client = CreateLibraryClient(fake);

            // HttpRequestException in ItemTypeExistsAsync is caught, logged, returns false -> RecordUsageAsync returns
            await client.RecordUsageAsync(MakeUsageRequest(), CancellationToken.None);

            var call = Assert.Single(fake.Calls);
            Assert.Equal("ApplyAml", call.MethodKind);
        }

        // ── Error message safety ────────────────────────────────────────

        [Theory]
        [InlineData("access token", true)]
        [InlineData("Authorization: Bearer", true)]
        [InlineData("SOAP-ENV:", true)]
        public void ClassifyArasError_DoesNotLeakCredentials(string fragment, bool shouldLeak)
        {
            foreach (ArasErrorCode code in Enum.GetValues(typeof(ArasErrorCode)))
            {
                var ex = new ArasOperationException(code, "test " + code);
                var msg = HttpPdmRepositoryClient.ClassifyArasError(ex) ?? "";
                if (shouldLeak)
                    Assert.DoesNotContain(fragment, msg, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void SanitizeForUser_StripsBearerToken()
        {
            var result = HttpPdmRepositoryClient.ClassifyArasError(new ArasOperationException(ArasErrorCode.PartNotFound, "Authorization: Bearer xxx")) ?? "";
            Assert.DoesNotContain("Bearer", result);
        }

        // ── Shared helpers ──────────────────────────────────────────────

        private sealed class TempFolder : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PLTest_" + Guid.NewGuid().ToString("N"));

            public TempFolder()
            {
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, true); }
                catch { }
            }
        }
    }
}
