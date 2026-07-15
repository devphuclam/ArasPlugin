using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace;
using IdeaCadConnector.Workspace.Clone;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmClonePackageBuilderTests
    {
        [Fact]
        public void Build_WritesPushCompatibleManifestAndBranchRegistry()
        {
            using var folder = new TempFolder();
            var cad = Path.Combine(folder.Path, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__A01__BASE.ics"), new byte[] { 2 });

            var result = new PdmClonePackageBuilder().Build(CreateInput(folder.Path));

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("cad/PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics", result.Manifest.RootFile);
            Assert.Equal(2, result.Manifest.Definitions.Count());
            Assert.Equal(2, result.Manifest.Occurrences.Count());
            Assert.Equal(2m, result.Manifest.BomV2.Single().Quantity);
            Assert.Equal("IdentityUnavailable", result.Manifest.BomV2.Single().QuantityStatus);
            Assert.Equal(new[] { "0", "0/0" }, result.Manifest.Occurrences.Select(occurrence => occurrence.OccurrencePath));
            Assert.True(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
            Assert.True(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "branches.json")));
            Assert.True(new PdmPackageImportReader().Read(folder.Path).Validation.IsValid);
        }

        [Theory]
        [InlineData("../escape.ics")]
        [InlineData("nested\\part.ics")]
        public void Build_RejectsUnsafeNativeFileName(string nativeFileName)
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Nodes.First(node => node.NodeId == "part-a01").NativeFileName = nativeFileName;

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.False(result.Success);
            Assert.Contains("NativeFileName", result.ErrorMessage);
        }

        [Fact]
        public void Build_RejectsDuplicateNativeFileName()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Nodes.Last().NativeFileName = input.Nodes.First().NativeFileName;

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.False(result.Success);
            Assert.Contains("duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_RejectsMissingCadFile()
        {
            using var folder = new TempFolder();
            var cad = Path.Combine(folder.Path, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });

            var result = new PdmClonePackageBuilder().Build(CreateInput(folder.Path));

            Assert.False(result.Success);
            Assert.Contains("missing", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_RejectsBomCycle()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Edges = new[]
            {
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 10 },
                new PdmCloneBomEdge { ParentNodeId = "part-a01", ChildNodeId = "root-part", Quantity = 1, SortOrder = 20 }
            };

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.False(result.Success);
            Assert.Contains("cycle", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_WritesOnlyNativeCadFilesAndWorkspaceMetadata()
        {
            using var folder = new TempFolder();
            var cad = Path.Combine(folder.Path, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__A01__BASE.ics"), new byte[] { 2 });

            var result = new PdmClonePackageBuilder().Build(CreateInput(folder.Path));

            Assert.True(result.Success, result.ErrorMessage);
            var allFiles = Directory.GetFiles(folder.Path, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetFileName(path))
                .ToArray();
            Assert.DoesNotContain(allFiles, file => file.IndexOf("ARAS01", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(allFiles, file => string.Equals(Path.GetExtension(file), ".dwg", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(allFiles, file => string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(allFiles, file => file.EndsWith("-STRUCTURE.txt", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_ReusesDefinitionForEachTraversedBomEdge()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Edges = new[]
            {
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 20 },
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 1, SortOrder = 10 }
            };

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.Manifest.Definitions.Count());
            Assert.Equal(new[] { "0", "0/0", "0/1" }, result.Manifest.Occurrences.Select(occurrence => occurrence.OccurrencePath));
            Assert.Equal(new[] { 1m, 2m }, result.Manifest.BomV2.Select(edge => edge.Quantity));
        }

        [Fact]
        public void Build_RegistersMainAndSelectedBranchOnce()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.BranchName = "feature/studycase";
            var result = new PdmClonePackageBuilder().Build(input);

            Assert.True(result.Success, result.ErrorMessage);
            var branches = new WorkspaceService(new WorkspaceOptions()).LoadBranchRegistry(folder.Path);
            Assert.Equal(new[] { "main", "feature/studycase" }, branches.Branches.Select(branch => branch.Name));
        }

        private static PdmClonePackageInput CreateInput(string packageRoot)
        {
            return new PdmClonePackageInput
            {
                PackageRoot = packageRoot,
                ProjectCode = "PDM-STUDYCASE",
                Revision = "A",
                BranchName = "main",
                RootNodeId = "root-part",
                Nodes = new[]
                {
                    new PdmCloneNode { NodeId = "root-part", ItemCode = "ROOT", ItemType = "ASM", DisplayName = "PDM-STUDYCASE", Revision = "A", NativeFileName = "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics" },
                    new PdmCloneNode { NodeId = "part-a01", ItemCode = "A01", ItemType = "PRT", DisplayName = "BASE", Revision = "A", NativeFileName = "PDM-STUDYCASE__A01__BASE.ics" }
                },
                Edges = new[] { new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 10 } }
            };
        }

        private static void WriteExpectedCadFiles(string packageRoot)
        {
            var cad = Path.Combine(packageRoot, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__A01__BASE.ics"), new byte[] { 2 });
        }

        private sealed class TempFolder : IDisposable
        {
            public TempFolder()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pdm-clone-builder-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
        }
    }
}
