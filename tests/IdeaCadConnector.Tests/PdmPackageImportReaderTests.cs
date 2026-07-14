using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmPackageImportReaderTests
    {
        [Fact]
        public void Read_MapsManifestV2IntoIdeaPdmAnalyses()
        {
            var folder = CreatePackage();
            try
            {
                var result = new PdmPackageImportReader().Read(folder);

                Assert.Equal("PDM-DEMO", result.FolderAnalysis.ProjectCode);
                Assert.Equal("A", result.FolderAnalysis.PrimaryAssembly.Revision);
                Assert.Equal("ROOT", result.FolderAnalysis.PrimaryAssembly.LogicalPartCode);
                Assert.Equal(2, result.FolderAnalysis.TrackedFiles.Count);
                Assert.Single(result.FolderAnalysis.DetailFiles);
                Assert.True(result.BusinessStructure.HasStructure);
                var child = Assert.Single(result.BusinessStructure.RootNodes);
                Assert.Equal("P01", child.Code);
                Assert.Equal("HANDLE", child.Name);
                Assert.Equal("Component", child.NodeType);
                Assert.Equal("part.ics", child.SourceFileName);
                Assert.True(result.Validation.IsValid);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void Read_DoesNotExposeManifestPathOutsidePackage()
        {
            var folder = CreatePackage();
            try
            {
                var path = Path.Combine(folder, "pdm-bom-manifest.json");
                var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<PdmPackageManifest>(File.ReadAllText(path));
                manifest.Definitions = manifest.Definitions.Select(definition =>
                {
                    if (definition.ItemCode == "P01") definition.FileName = "../outside.ics";
                    return definition;
                }).ToArray();
                File.WriteAllText(path, new PdmPackageManifestWriter().Serialize(manifest));

                var result = new PdmPackageImportReader().Read(folder);

                var unsafeFile = Assert.Single(result.FolderAnalysis.DetailFiles);
                Assert.Null(unsafeFile.FullPath);
                Assert.Contains(PdmPackageValidationIssue.InvalidManifestPath, result.Validation.Issues);
                Assert.Contains(result.FolderAnalysis.Issues, issue => issue.BlocksPush);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string CreatePackage()
        {
            var folder = Path.Combine(Path.GetTempPath(), "pdm-import-" + Guid.NewGuid().ToString("N"));
            var cad = Path.Combine(folder, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllText(Path.Combine(cad, "root.ics"), "root");
            File.WriteAllText(Path.Combine(cad, "part.ics"), "part");
            var manifest = new PdmPackageManifest
            {
                SchemaVersion = 2,
                ProjectCode = "PDM-DEMO",
                Revision = "A",
                RootNodeId = "node-root",
                RootItemCode = "ROOT",
                RootFile = "cad/root.ics",
                RootOccurrenceId = "occ-root",
                Definitions = new[]
                {
                    new PdmManifestDefinition { DefinitionId = "def-root", NodeId = "node-root", ItemCode = "ROOT", ItemType = "ASM", DisplayName = "DEMO", Revision = "A", FileName = "cad/root.ics" },
                    new PdmManifestDefinition { DefinitionId = "def-part", NodeId = "node-part", ItemCode = "P01", ItemType = "PRT", DisplayName = "HANDLE", Revision = "A", FileName = "cad/part.ics" }
                },
                Occurrences = new[]
                {
                    new PdmManifestOccurrence { OccurrenceId = "occ-root", OccurrencePath = "0", DefinitionId = "def-root", FindNumber = 10 },
                    new PdmManifestOccurrence { OccurrenceId = "occ-part", OccurrencePath = "0/0", ParentOccurrenceId = "occ-root", DefinitionId = "def-part", FindNumber = 10 }
                },
                BomV2 = new[]
                {
                    new PdmManifestBomV2 { ParentOccurrenceId = "occ-root", ChildDefinitionId = "def-part", Quantity = 1, QuantityStatus = "IdentityUnavailable" }
                }
            };
            File.WriteAllText(Path.Combine(folder, "pdm-bom-manifest.json"), new PdmPackageManifestWriter().Serialize(manifest));
            return folder;
        }
    }
}
