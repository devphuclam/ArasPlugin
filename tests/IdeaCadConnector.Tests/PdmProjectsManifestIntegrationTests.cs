using System;
using System.IO;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmProjectsManifestIntegrationTests
    {
        [Fact]
        public void AnalyzeFolder_PrefersManifestV2AndBuildsExistingBomTree()
        {
            var folder = CreatePackage();
            try
            {
                var viewModel = new PdmProjectsViewModel { FolderPath = folder };

                viewModel.AnalyzeFolderCommand.Execute(null);

                Assert.Equal("pdm-manifest-v2", viewModel.NamingPolicyVersion);
                var root = Assert.Single(viewModel.PdmStructure);
                Assert.Equal("PDM-DEMO", root.PartCode);
                Assert.Equal("root.ics", root.PrimaryCad);
                var assembly = Assert.Single(root.Children);
                Assert.Equal("A01", assembly.PartCode);
                Assert.Equal("SUB-ASSEMBLY", assembly.Name);
                Assert.Equal("sub.ics", assembly.PrimaryCad);
                var child = Assert.Single(assembly.Children);
                Assert.Equal("P01", child.PartCode);
                Assert.Equal("HANDLE", child.Name);
                Assert.Equal("part.ics", child.PrimaryCad);
                Assert.Contains("2 assembly", viewModel.AnalysisSummary, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("1 part", viewModel.AnalysisSummary, StringComparison.OrdinalIgnoreCase);
                Assert.Empty(viewModel.PreviewDocuments);
                Assert.Equal(0, viewModel.PushPreviewReadiness.BlockingIssueCount);
                Assert.True(viewModel.PushPreviewReadiness.CanPush);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string CreatePackage()
        {
            var folder = Path.Combine(Path.GetTempPath(), "pdm-desktop-import-" + Guid.NewGuid().ToString("N"));
            var cad = Path.Combine(folder, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllText(Path.Combine(cad, "root.ics"), "root");
            File.WriteAllText(Path.Combine(cad, "sub.ics"), "sub");
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
                    new PdmManifestDefinition { DefinitionId = "def-sub", NodeId = "node-sub", ItemCode = "A01", ItemType = "ASM", DisplayName = "SUB-ASSEMBLY", Revision = "A", FileName = "cad/sub.ics" },
                    new PdmManifestDefinition { DefinitionId = "def-part", NodeId = "node-part", ItemCode = "P01", ItemType = "PRT", DisplayName = "HANDLE", Revision = "A", FileName = "cad/part.ics" }
                },
                Occurrences = new[]
                {
                    new PdmManifestOccurrence { OccurrenceId = "occ-root", OccurrencePath = "0", DefinitionId = "def-root", FindNumber = 10 },
                    new PdmManifestOccurrence { OccurrenceId = "occ-sub", OccurrencePath = "0/0", ParentOccurrenceId = "occ-root", DefinitionId = "def-sub", FindNumber = 10 },
                    new PdmManifestOccurrence { OccurrenceId = "occ-part", OccurrencePath = "0/0/0", ParentOccurrenceId = "occ-sub", DefinitionId = "def-part", FindNumber = 10 }
                },
                BomV2 = new[]
                {
                    new PdmManifestBomV2 { ParentOccurrenceId = "occ-root", ChildDefinitionId = "def-sub", Quantity = 1, QuantityStatus = "IdentityUnavailable" },
                    new PdmManifestBomV2 { ParentOccurrenceId = "occ-sub", ChildDefinitionId = "def-part", Quantity = 1, QuantityStatus = "IdentityUnavailable" }
                }
            };
            File.WriteAllText(Path.Combine(folder, PdmPackageImportReader.ManifestFileName), new PdmPackageManifestWriter().Serialize(manifest));
            return folder;
        }
    }
}
