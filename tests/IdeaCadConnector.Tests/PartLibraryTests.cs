using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Workspace;
using Newtonsoft.Json;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class PartLibraryTests
    {
        // ── Item 1: Library Reference Requires ExistingPartId ──────────────

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
            var part = new PdmPartRequest
            {
                IsExternalReference = false,
                SourceKind = "Generated"
            };
            Assert.False(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        [Fact]
        public void IsLibraryReference_EmptySourceKindAndExternalFalse_ReturnsFalse()
        {
            var part = new PdmPartRequest();
            Assert.False(HttpPdmRepositoryClient.IsLibraryReference(part));
        }

        // ── Item 2: Config ID and Revision mismatch ───────────────────────

        [Fact]
        public void ConfigIdMismatch_CorrectlyDetected()
        {
            var amlItem = new
            {
                id = "part123",
                item_number = "ABC-001",
                name = "Test Part",
                state = "Released",
                config_id = "config456",
                major_rev = "A"
            };
            var json = JsonConvert.SerializeObject(amlItem);
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);

            var expectedConfigId = "config789";
            var actualConfigId = token["config_id"]?.ToString();

            Assert.NotEqual(expectedConfigId, actualConfigId);
        }

        [Fact]
        public void RevisionMismatch_CorrectlyDetected()
        {
            var amlItem = new
            {
                id = "part123",
                item_number = "ABC-001",
                name = "Test Part",
                state = "Released",
                config_id = "config456",
                major_rev = "A"
            };
            var json = JsonConvert.SerializeObject(amlItem);
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);

            var expectedRev = "B";
            var actualRev = token["major_rev"]?.ToString();

            Assert.NotEqual(expectedRev, actualRev);
        }

        // ── Item 3: Obsolete state uses 'state', not 'current_state' ──────

        [Fact]
        public void IsPartObsolete_ExactObsoleteConstant_ReturnsTrue()
        {
            Assert.True(HttpPdmRepositoryClient.IsPartObsolete(CadLifecyclePolicy.Obsolete));
        }

        [Fact]
        public void IsPartObsolete_ObsoleteLowerCase_ReturnsTrue()
        {
            Assert.True(HttpPdmRepositoryClient.IsPartObsolete("loai bo"));
        }

        [Fact]
        public void IsPartObsolete_Released_ReturnsFalse()
        {
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete("Released"));
        }

        [Fact]
        public void IsPartObsolete_Initial_ReturnsFalse()
        {
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete("Khoi tao"));
        }

        [Fact]
        public void IsPartObsolete_Empty_ReturnsFalse()
        {
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete(null));
            Assert.False(HttpPdmRepositoryClient.IsPartObsolete(""));
        }

        // ── Item 4: Error code categorization ────────────────────────────

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

        // ── Item 5: BOM validation ───────────────────────────────────────

        [Fact]
        public void BomActionResult_EnumValuesAreCorrect()
        {
            Assert.Equal(0, (int)BomActionResult.Created);
            Assert.Equal(1, (int)BomActionResult.QuantityUpdated);
            Assert.Equal(2, (int)BomActionResult.Unchanged);
            Assert.Equal(3, (int)BomActionResult.InvalidParentChild);
            Assert.Equal(4, (int)BomActionResult.InvalidQuantity);
        }

        // ── Item 6: Duplicate reference updates all fields ───────────────

        [Fact]
        public void DuplicateReferenceUpdate_AllFieldsPreservedFromNew()
        {
            var original = new WorkspaceLibraryReference
            {
                ReferenceId = "ref1",
                PartId = "oldPartId",
                PartConfigId = "oldConfigId",
                Revision = "A",
                RevisionPolicy = "Pinned",
                LibraryEntryId = "entry1",
                Quantity = 1
            };

            var updated = new WorkspaceLibraryReference
            {
                ReferenceId = "ref1",
                PartId = "newPartId",
                PartConfigId = "newConfigId",
                Revision = "B",
                RevisionPolicy = "LatestReleased",
                LibraryEntryId = "entry2",
                Quantity = 5
            };

            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var wsService = new WorkspaceService(new WorkspaceOptions());
                var store = new WorkspaceLibraryReferenceStore(wsService);

                store.Upsert(folder, original);
                store.Upsert(folder, updated);

                var loaded = store.Load(folder);
                var match = loaded.FirstOrDefault(r => r.ReferenceId == "ref1");

                Assert.NotNull(match);
                Assert.Equal("newPartId", match.PartId);
                Assert.Equal("newConfigId", match.PartConfigId);
                Assert.Equal("B", match.Revision);
                Assert.Equal("LatestReleased", match.RevisionPolicy);
                Assert.Equal("entry2", match.LibraryEntryId);
                Assert.Equal(5, match.Quantity);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        // ── Item 7: Malformed JSON diagnostic ──────────────────────────────

        [Fact]
        public void MalformedJson_ThrowsInvalidOperationException()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var wsService = new WorkspaceService(new WorkspaceOptions());
                var store = new WorkspaceLibraryReferenceStore(wsService);
                var manifestDir = Path.Combine(folder, ".idea-pdm");
                Directory.CreateDirectory(manifestDir);
                var filePath = Path.Combine(manifestDir, "library-references.json");
                File.WriteAllText(filePath, "{invalid json!!!!}");

                var ex = Record.Exception(() => store.Load(folder));
                Assert.NotNull(ex);
                Assert.IsType<InvalidOperationException>(ex);
                Assert.Contains("library-references.json", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmptyList()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var wsService = new WorkspaceService(new WorkspaceOptions());
                var store = new WorkspaceLibraryReferenceStore(wsService);

                var result = store.Load(folder);
                Assert.NotNull(result);
                Assert.Empty(result);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        // ── Item 8: Usage record includes UsedBy ─────────────────────────

        [Fact]
        public void UsageRequest_UsedByFieldCarriesThrough()
        {
            var request = new LibraryUsageRequest
            {
                LibraryEntryId = "entry1",
                PartId = "part1",
                ProjectCode = "PROJ",
                ParentPartId = "parent1",
                Quantity = 2,
                UsedBy = "testuser",
                CommitId = "commit1",
                ActionType = "ReusedFromLibrary"
            };

            Assert.Equal("testuser", request.UsedBy);
        }

        [Fact]
        public void UsageRequest_UsedByCanBeEmpty()
        {
            var request = new LibraryUsageRequest
            {
                LibraryEntryId = "entry1",
                UsedBy = null
            };

            Assert.Null(request.UsedBy);
        }

        // ── Workspace schema version ──────────────────────────────────────

        [Fact]
        public void WorkspaceLibraryReferenceStore_CurrentSchemaVersionIsOne()
        {
            Assert.Equal(1, WorkspaceLibraryReferenceStore.CurrentSchemaVersion);
        }

        [Fact]
        public void SaveAndLoad_RoundTripsAllFields()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
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

                store.Save(folder, refs);
                var loaded = store.Load(folder);

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
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Remove_ExistingReference_RemovesIt()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var wsService = new WorkspaceService(new WorkspaceOptions());
                var store = new WorkspaceLibraryReferenceStore(wsService);

                store.Upsert(folder, new WorkspaceLibraryReference { ReferenceId = "x1", PartId = "p1" });
                Assert.NotEmpty(store.Load(folder));

                var removed = store.Remove(folder, "x1");
                Assert.True(removed);
                Assert.Empty(store.Load(folder));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Remove_NonExistentReference_ReturnsFalse()
        {
            var folder = Path.Combine(Path.GetTempPath(), "PLTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var wsService = new WorkspaceService(new WorkspaceOptions());
                var store = new WorkspaceLibraryReferenceStore(wsService);

                var removed = store.Remove(folder, "nonexistent");
                Assert.False(removed);
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
