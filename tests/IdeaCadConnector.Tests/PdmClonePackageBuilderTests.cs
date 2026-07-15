using System;
using System.Collections;
using System.Collections.Generic;
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
            var imported = new PdmPackageImportReader().Read(folder.Path);
            Assert.True(imported.Validation.IsValid);
            Assert.Equal(result.Manifest.RootFile, imported.Manifest.RootFile);
            Assert.Equal(result.Manifest.Definitions.Count(), imported.Manifest.Definitions.Count());
            Assert.Equal(result.Manifest.Occurrences.Count(), imported.Manifest.Occurrences.Count());
            Assert.Equal(2m, imported.Manifest.BomV2.Single().Quantity);
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
            Assert.Empty(Directory.GetFiles(folder.Path, "*.ics", SearchOption.TopDirectoryOnly));
        }

        [Fact]
        public void Build_MaterializesOneShotInputEnumerablesOnce()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Nodes = new SingleUseEnumerable<PdmCloneNode>(input.Nodes);
            input.Edges = new SingleUseEnumerable<PdmCloneBomEdge>(input.Edges);

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.True(result.Success, result.ErrorMessage);
        }

        [Fact]
        public void Build_TreatsNullEdgesAsEmpty()
        {
            using var folder = new TempFolder();
            var cad = Path.Combine(folder.Path, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });

            var input = CreateInput(folder.Path);
            input.Nodes = input.Nodes.Take(1).ToArray();
            input.Edges = null;

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Empty(result.Manifest.BomV2);
        }

        [Fact]
        public void Build_UsesStableManifestOrderingWhenEdgesAreReversed()
        {
            using var firstFolder = new TempFolder();
            using var secondFolder = new TempFolder();
            WriteExpectedCadFiles(firstFolder.Path);
            WriteExpectedCadFiles(secondFolder.Path);
            WriteCadFile(firstFolder.Path, "PDM-STUDYCASE__B01__CAP.ics");
            WriteCadFile(secondFolder.Path, "PDM-STUDYCASE__B01__CAP.ics");

            var first = CreateInputWithTwoChildren(firstFolder.Path, reverseEdges: false);
            var second = CreateInputWithTwoChildren(secondFolder.Path, reverseEdges: true);

            var firstResult = new PdmClonePackageBuilder().Build(first);
            var secondResult = new PdmClonePackageBuilder().Build(second);

            Assert.True(firstResult.Success, firstResult.ErrorMessage);
            Assert.True(secondResult.Success, secondResult.ErrorMessage);
            Assert.Equal(
                firstResult.Manifest.Occurrences.Select(occurrence => occurrence.OccurrencePath),
                secondResult.Manifest.Occurrences.Select(occurrence => occurrence.OccurrencePath));
            Assert.Equal(
                new PdmPackageManifestWriter().Serialize(firstResult.Manifest),
                new PdmPackageManifestWriter().Serialize(secondResult.Manifest));
        }

        [Fact]
        public void Build_RejectsDuplicateEdgeOrderingIdentity()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Edges = new[]
            {
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 1, SortOrder = 10 },
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 10 }
            };

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.False(result.Success);
            Assert.Contains("ordering", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Build_RemovesGeneratedArtifactsWhenManifestValidationFails()
        {
            using var folder = new TempFolder();
            WriteExpectedCadFiles(folder.Path);

            var input = CreateInput(folder.Path);
            input.Nodes.Last().ItemCode = input.Nodes.First().ItemCode;

            var result = new PdmClonePackageBuilder().Build(input);

            Assert.False(result.Success);
            Assert.Contains("DuplicateItemCode", result.ErrorMessage);
            Assert.False(File.Exists(Path.Combine(folder.Path, "pdm-bom-manifest.json")));
            Assert.False(File.Exists(Path.Combine(folder.Path, ".idea-pdm", "branches.json")));
            Assert.False(Directory.Exists(Path.Combine(folder.Path, ".idea-pdm")));
            Assert.True(File.Exists(Path.Combine(folder.Path, "cad", "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics")));
            Assert.True(File.Exists(Path.Combine(folder.Path, "cad", "PDM-STUDYCASE__A01__BASE.ics")));
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

        private static PdmClonePackageInput CreateInputWithTwoChildren(string packageRoot, bool reverseEdges)
        {
            var input = CreateInput(packageRoot);
            input.Nodes = input.Nodes.Concat(new[]
            {
                new PdmCloneNode { NodeId = "part-b01", ItemCode = "B01", ItemType = "PRT", DisplayName = "CAP", Revision = "A", NativeFileName = "PDM-STUDYCASE__B01__CAP.ics" }
            }).ToArray();
            input.Edges = new[]
            {
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-a01", Quantity = 2, SortOrder = 10 },
                new PdmCloneBomEdge { ParentNodeId = "root-part", ChildNodeId = "part-b01", Quantity = 1, SortOrder = 20 }
            };
            if (reverseEdges)
                input.Edges = input.Edges.Reverse().ToArray();
            return input;
        }

        private static void WriteExpectedCadFiles(string packageRoot)
        {
            var cad = Path.Combine(packageRoot, "cad");
            Directory.CreateDirectory(cad);
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__ROOT__PDM-STUDYCASE.ics"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(cad, "PDM-STUDYCASE__A01__BASE.ics"), new byte[] { 2 });
        }

        private static void WriteCadFile(string packageRoot, string fileName)
        {
            File.WriteAllBytes(Path.Combine(packageRoot, "cad", fileName), new byte[] { 3 });
        }

        private sealed class SingleUseEnumerable<T> : IEnumerable<T>
        {
            private readonly IEnumerable<T> _items;
            private bool _wasEnumerated;

            public SingleUseEnumerable(IEnumerable<T> items)
            {
                _items = items;
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (_wasEnumerated)
                    throw new InvalidOperationException("Input enumerable was read more than once.");
                _wasEnumerated = true;
                return _items.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
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
