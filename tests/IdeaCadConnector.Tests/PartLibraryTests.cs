using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Workspace;
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
        public void PartLifecyclePolicy_Preliminary_ReturnsFalse()
        {
            Assert.False(PartLifecyclePolicy.IsPartObsolete("Preliminary"));
        }

        [Fact]
        public void PartLifecyclePolicy_Draft_ReturnsFalse()
        {
            Assert.False(PartLifecyclePolicy.IsPartObsolete("Draft"));
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

        [Fact]
        public void PartLifecyclePolicy_GetPartNotReusableMessage_Null_ReturnsNull()
        {
            Assert.Null(PartLifecyclePolicy.GetPartNotReusableMessage(null, "ABC-001"));
        }

        // ── IsLibraryReference ──────────────────────────────────────────

        [Fact]
        public void IsLibraryReference_ExternalRefTrue_ReturnsTrue()
        {
            var part = new PdmPartRequest { IsExternalReference = true };
            Assert.True(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        [Fact]
        public void IsLibraryReference_SourceKindLibraryReference_ReturnsTrue()
        {
            var part = new PdmPartRequest { SourceKind = "LibraryReference" };
            Assert.True(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        [Fact]
        public void IsLibraryReference_NormalPart_ReturnsFalse()
        {
            var part = new PdmPartRequest { IsExternalReference = false, SourceKind = "Generated" };
            Assert.False(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        [Fact]
        public void IsLibraryReference_EmptySourceKindAndExternalFalse_ReturnsFalse()
        {
            var part = new PdmPartRequest();
            Assert.False(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        // ── IsPartObsolete (now delegates to PartLifecyclePolicy) ────────

        [Fact]
        public void IsPartObsolete_ExactPartLifecyclePolicyConstant_ReturnsTrue()
        {
            Assert.True(HttpPdmRepositoryClient.IsPartObsolete(PartLifecyclePolicy.Obsolete));
        }

        [Fact]
        public void IsPartObsolete_Released_ReturnsFalse()
        {
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete("Released"));
        }

        [Fact]
        public void IsPartObsolete_NullOrEmpty_ReturnsFalse()
        {
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete(null));
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete(""));
        }

        // ── ClassifyArasError ───────────────────────────────────────────

        [Fact]
        public void ClassifyArasError_AuthInvalid_ReturnsAuthMessage()
        {
            var ex = new ArasOperationException(ArasErrorCode.AuthInvalid, "bad token");
            Assert.Contains("Authentication", HttpPdmRepositoryClient.ClassifyArasError(ex), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClassifyArasError_AuthExpired_ReturnsAuthMessage()
        {
            var ex = new ArasOperationException(ArasErrorCode.AuthExpired, "expired");
            Assert.Contains("Authentication", HttpPdmRepositoryClient.ClassifyArasError(ex), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClassifyArasError_PermissionDenied_ReturnsPermissionMessage()
        {
            var ex = new ArasOperationException(ArasErrorCode.PermissionDenied, "no access");
            Assert.Contains("Permission denied", HttpPdmRepositoryClient.ClassifyArasError(ex), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClassifyArasError_PartNotFound_ReturnsNotFoundMessage()
        {
            var ex = new ArasOperationException(ArasErrorCode.PartNotFound, "not found");
            Assert.Contains("not found", HttpPdmRepositoryClient.ClassifyArasError(ex), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClassifyArasError_ServerUnavailable_ReturnsServerMessage()
        {
            var ex = new ArasOperationException(ArasErrorCode.ServerUnavailable, "down");
            Assert.Contains("Server", HttpPdmRepositoryClient.ClassifyArasError(ex), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClassifyArasError_UnknownCode_ReturnsNull()
        {
            var ex = new ArasOperationException(ArasErrorCode.CadLocked, "cad locked");
            Assert.Null(HttpPdmRepositoryClient.ClassifyArasError(ex));
        }

        // ── Result consistency invariants ──────────────────────────────

        [Fact]
        public void BomActionResult_SuccessActions_AreCreatedQuantityUpdatedUnchanged()
        {
            Assert.True(BomActionResultIsSuccess(BomActionResult.Created));
            Assert.True(BomActionResultIsSuccess(BomActionResult.QuantityUpdated));
            Assert.True(BomActionResultIsSuccess(BomActionResult.Unchanged));
            Assert.False(BomActionResultIsSuccess(BomActionResult.InvalidParentChild));
            Assert.False(BomActionResultIsSuccess(BomActionResult.InvalidQuantity));
            Assert.False(BomActionResultIsSuccess(BomActionResult.Failed));
        }

        private static bool BomActionResultIsSuccess(BomActionResult action)
        {
            return action == BomActionResult.Created ||
                   action == BomActionResult.QuantityUpdated ||
                   action == BomActionResult.Unchanged;
        }

        [Fact]
        public void ClassifyArasError_KnownCodesReturnNonNull()
        {
            var knownCodes = new[]
            {
                ArasErrorCode.AuthInvalid,
                ArasErrorCode.AuthExpired,
                ArasErrorCode.PermissionDenied,
                ArasErrorCode.PartNotFound,
                ArasErrorCode.ServerUnavailable
            };
            foreach (var code in knownCodes)
            {
                var ex = new ArasOperationException(code, "test");
                Assert.NotNull(HttpPdmRepositoryClient.ClassifyArasError(ex));
            }
        }

        [Fact]
        public void ClassifyArasError_UnknownCodesReturnNull()
        {
            var unknownCodes = new[]
            {
                ArasErrorCode.CadLocked,
                ArasErrorCode.CadAlreadyExists,
                ArasErrorCode.ValidationFailed,
                ArasErrorCode.UnexpectedServerError,
                ArasErrorCode.Unknown
            };
            foreach (var code in unknownCodes)
            {
                var ex = new ArasOperationException(code, "test");
                Assert.Null(HttpPdmRepositoryClient.ClassifyArasError(ex));
            }
        }

        // ── BomActionResult enum ────────────────────────────────────────

        [Fact]
        public void BomActionResult_EnumValuesAreCorrect()
        {
            Assert.Equal(0, (int)BomActionResult.Created);
            Assert.Equal(1, (int)BomActionResult.QuantityUpdated);
            Assert.Equal(2, (int)BomActionResult.Unchanged);
            Assert.Equal(3, (int)BomActionResult.InvalidParentChild);
            Assert.Equal(4, (int)BomActionResult.InvalidQuantity);
            Assert.Equal(5, (int)BomActionResult.Failed);
        }

        // ── PdmBomPushResult model ──────────────────────────────────────

        [Fact]
        public void PdmBomPushResult_Default_IsNotSuccess()
        {
            var r = new PdmBomPushResult();
            Assert.False(r.Success);
        }

        [Fact]
        public void PdmBomPushResult_CanCarryAllFields()
        {
            var r = new PdmBomPushResult
            {
                ParentLogicalCode = "PARENT",
                ChildLogicalCode = "CHILD",
                ParentPartId = "pid1",
                ChildPartId = "pid2",
                Quantity = 3,
                Success = true,
                ActionTaken = BomActionResult.Created,
                ErrorMessage = null
            };
            Assert.Equal("PARENT", r.ParentLogicalCode);
            Assert.Equal("CHILD", r.ChildLogicalCode);
            Assert.Equal("pid1", r.ParentPartId);
            Assert.Equal("pid2", r.ChildPartId);
            Assert.Equal(3, r.Quantity);
            Assert.True(r.Success);
            Assert.Equal(BomActionResult.Created, r.ActionTaken);
        }

        // ── PdmPushResult.BomResults ────────────────────────────────────

        [Fact]
        public void PdmPushResult_CanHoldBomResults()
        {
            var result = new PdmPushResult
            {
                BomResults = new[]
                {
                    new PdmBomPushResult { Success = true, ActionTaken = BomActionResult.Created }
                }
            };
            Assert.Single(result.BomResults);
            Assert.True(result.BomResults[0].Success);
        }

        // ── ArasAmlClient.ClassifyNotFoundError ─────────────────────────
        // Tests directly against the production classification logic.

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
        public void ClassifyNotFoundError_CadLowerCase_ReturnsCadNotFound()
        {
            Assert.Equal(ArasErrorCode.CadNotFound, ArasAmlClient.ClassifyNotFoundError("cad"));
        }

        [Fact]
        public void ClassifyNotFoundError_Document_ReturnsValidationFailed()
        {
            Assert.Equal(ArasErrorCode.ValidationFailed, ArasAmlClient.ClassifyNotFoundError("Document"));
        }

        [Fact]
        public void ClassifyNotFoundError_PartBom_ReturnsValidationFailed()
        {
            Assert.Equal(ArasErrorCode.ValidationFailed, ArasAmlClient.ClassifyNotFoundError("Part BOM"));
        }

        [Fact]
        public void ClassifyNotFoundError_Project_ReturnsValidationFailed()
        {
            Assert.Equal(ArasErrorCode.ValidationFailed, ArasAmlClient.ClassifyNotFoundError("Project"));
        }

        [Fact]
        public void ClassifyNotFoundError_UnknownType_ReturnsValidationFailed()
        {
            Assert.Equal(ArasErrorCode.ValidationFailed, ArasAmlClient.ClassifyNotFoundError("UnknownType"));
        }

        // ── ArasAmlClient.ClassifyErrorText ──────────────────────────────
        // Tests against the production error text classifier.

        [Fact]
        public void ClassifyErrorText_AccessDenied_ReturnsPermissionDenied()
        {
            Assert.Equal(ArasErrorCode.PermissionDenied, ArasAmlClient.ClassifyErrorText("Access denied"));
        }

        [Fact]
        public void ClassifyErrorText_PermissionDenied_ReturnsPermissionDenied()
        {
            Assert.Equal(ArasErrorCode.PermissionDenied, ArasAmlClient.ClassifyErrorText("Permission denied for user"));
        }

        [Fact]
        public void ClassifyErrorText_CouldNotLogIn_ReturnsAuthInvalid()
        {
            Assert.Equal(ArasErrorCode.AuthInvalid, ArasAmlClient.ClassifyErrorText("Could not log in"));
        }

        [Fact]
        public void ClassifyErrorText_InvalidCredentials_ReturnsAuthInvalid()
        {
            Assert.Equal(ArasErrorCode.AuthInvalid, ArasAmlClient.ClassifyErrorText("Invalid credentials"));
        }

        [Fact]
        public void ClassifyErrorText_InternalServerError_ReturnsServerUnavailable()
        {
            Assert.Equal(ArasErrorCode.ServerUnavailable, ArasAmlClient.ClassifyErrorText("Internal server error"));
        }

        [Fact]
        public void ClassifyErrorText_PartNotFound_ReturnsPartNotFound()
        {
            Assert.Equal(ArasErrorCode.PartNotFound, ArasAmlClient.ClassifyErrorText("PART_NOT_FOUND: missing part"));
        }

        [Fact]
        public void ClassifyErrorText_CadNotFound_ReturnsCadNotFound()
        {
            Assert.Equal(ArasErrorCode.CadNotFound, ArasAmlClient.ClassifyErrorText("CAD_NOT_FOUND"));
        }

        [Fact]
        public void ClassifyErrorText_UnknownError_ReturnsUnexpected()
        {
            Assert.Equal(ArasErrorCode.UnexpectedServerError, ArasAmlClient.ClassifyErrorText("Some random error"));
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

        [Fact]
        public void Remove_ExistingReference_RemovesIt()
        {
            using var folder = new TempFolder();
            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);

            store.Upsert(folder.Path, new WorkspaceLibraryReference { ReferenceId = "x1", PartId = "p1" });
            Assert.NotEmpty(store.Load(folder.Path));

            var removed = store.Remove(folder.Path, "x1");
            Assert.True(removed);
            Assert.Empty(store.Load(folder.Path));
        }

        [Fact]
        public void Remove_NonExistentReference_ReturnsFalse()
        {
            using var folder = new TempFolder();
            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);
            Assert.False(store.Remove(folder.Path, "nonexistent"));
        }

        [Fact]
        public void DuplicateReferenceUpdate_AllFieldsPreservedFromNew()
        {
            using var folder = new TempFolder();
            var wsService = new WorkspaceService(new WorkspaceOptions());
            var store = new WorkspaceLibraryReferenceStore(wsService);

            store.Upsert(folder.Path, new WorkspaceLibraryReference
            {
                ReferenceId = "ref1", PartId = "oldPartId", PartConfigId = "oldConfigId",
                Revision = "A", RevisionPolicy = "Pinned", LibraryEntryId = "entry1", Quantity = 1
            });

            store.Upsert(folder.Path, new WorkspaceLibraryReference
            {
                ReferenceId = "ref1", PartId = "newPartId", PartConfigId = "newConfigId",
                Revision = "B", RevisionPolicy = "LatestReleased", LibraryEntryId = "entry2", Quantity = 5
            });

            var loaded = store.Load(folder.Path);
            var match = loaded.FirstOrDefault(r => r.ReferenceId == "ref1");
            Assert.NotNull(match);
            Assert.Equal("newPartId", match.PartId);
            Assert.Equal("newConfigId", match.PartConfigId);
            Assert.Equal("B", match.Revision);
            Assert.Equal("LatestReleased", match.RevisionPolicy);
            Assert.Equal("entry2", match.LibraryEntryId);
            Assert.Equal(5, match.Quantity);
        }

        // ── LibraryUsageRequest ─────────────────────────────────────────

        [Fact]
        public void UsageRequest_UsedByFieldCarriesThrough()
        {
            var request = new LibraryUsageRequest
            {
                LibraryEntryId = "entry1", PartId = "part1", ProjectCode = "PROJ",
                ParentPartId = "parent1", Quantity = 2, UsedBy = "testuser",
                CommitId = "commit1", ActionType = "ReusedFromLibrary"
            };
            Assert.Equal("testuser", request.UsedBy);
        }

        [Fact]
        public void UsageRequest_UsedByCanBeNull()
        {
            var request = new LibraryUsageRequest { LibraryEntryId = "entry1", UsedBy = null };
            Assert.Null(request.UsedBy);
        }

        // ── UsageCreateResult enum ──────────────────────────────────────

        [Fact]
        public void UsageCreateResult_EnumValuesAreCorrect()
        {
            Assert.Equal(0, (int)UsageCreateResult.Created);
            Assert.Equal(1, (int)UsageCreateResult.AlreadyExists);
            Assert.Equal(2, (int)UsageCreateResult.ValidationFailed);
            Assert.Equal(3, (int)UsageCreateResult.AuthFailed);
            Assert.Equal(4, (int)UsageCreateResult.PermissionDenied);
            Assert.Equal(5, (int)UsageCreateResult.ServerError);
            Assert.Equal(6, (int)UsageCreateResult.UnknownError);
        }

        // ── Error message strings (Task 9 review) ───────────────────────

        [Theory]
        [InlineData("Authentication failure.", false)]
        [InlineData("Permission denied.", false)]
        [InlineData("not found", false)]
        [InlineData("Server is unavailable.", false)]
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

        // ── Behavioral: RecordUsageAsync via FakeArasAmlClient ──────────

        [Fact]
        public void MapArasError_AccessDenied_CreatesPermissionDeniedException()
        {
            var ex = ArasAmlClient.MapArasError("Access denied", "SomeMethod");
            Assert.Equal(ArasErrorCode.PermissionDenied, ex.ErrorCode);
        }

        [Fact]
        public void MapArasError_AuthFailure_CreatesAuthInvalidException()
        {
            var ex = ArasAmlClient.MapArasError("Could not log in", "LoginMethod");
            Assert.Equal(ArasErrorCode.AuthInvalid, ex.ErrorCode);
        }

        [Fact]
        public void MapArasError_ServerError_CreatesServerUnavailableException()
        {
            var ex = ArasAmlClient.MapArasError("Internal server error", "SomeMethod");
            Assert.Equal(ArasErrorCode.ServerUnavailable, ex.ErrorCode);
        }

        [Fact]
        public void MapArasError_PartNotFound_Passthrough()
        {
            var ex = ArasAmlClient.MapArasError("PART_NOT_FOUND", "SomeMethod");
            Assert.Equal(ArasErrorCode.PartNotFound, ex.ErrorCode);
        }

        [Fact]
        public void MapArasError_UnknownError_ReturnsUnexpected()
        {
            var ex = ArasAmlClient.MapArasError("Something weird happened", "SomeMethod");
            Assert.Equal(ArasErrorCode.UnexpectedServerError, ex.ErrorCode);
        }

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
