using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace.BomDiagnostic;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class BomDiagnosticTreeAnalyzerTests
    {
        [Fact]
        public void Analyze_PreservesParentChildHierarchyAndDepthFirstOrder()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("child-a", "Part", "part-a"),
                Node("child-b", "Assembly", "sub-def",
                    Node("grandchild", "Part", "part-b")));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(new[] { "root", "child-a", "child-b", "grandchild" },
                result.DepthFirstNodes.Select(node => node.RuntimeId).ToArray());
            Assert.Equal("root", result.RootNode.RuntimeId);
            Assert.Equal("child-b", result.RootNode.Children[1].RuntimeId);
            Assert.Equal(2, result.RootNode.Children[1].Children[0].Depth);
            Assert.Equal("child-b", result.RootNode.Children[1].Children[0].ParentRuntimeId);
        }

        [Fact]
        public void Analyze_TreatsNullChildrenAsEmptyAndSceneRootAsNonAssembly()
        {
            var root = Node("root", "SceneRoot", null);

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Empty(result.RootNode.Children);
            Assert.Equal("SceneRoot", result.RootNode.NodeKind);
            Assert.Equal(1, result.SceneRootCount);
            Assert.Equal(0, result.TechnicalOrUnknownCount);
        }

        [Fact]
        public void Analyze_ProtectsAgainstCyclesAndWarnsForDuplicateRuntimeIds()
        {
            var root = Node("root", "Assembly", "root-def");
            var child = Node("duplicate", "Part", "part-def");
            var repeatedRuntimeId = Node("duplicate", "Part", "part-def");
            root.Children.Add(child);
            child.Children.Add(root);
            root.Children.Add(repeatedRuntimeId);

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(3, result.DepthFirstNodes.Count);
            Assert.Contains(result.Warnings, warning => warning.Contains("cycle"));
            Assert.Contains(result.Warnings, warning => warning.Contains("duplicate runtime ID"));
        }

        [Fact]
        public void Analyze_GroupsRepeatedDefinitionsPerParent()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("one", "Part", "shared-def"),
                Node("two", "Part", "shared-def"),
                Node("three", "Part", "other-def"));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            var quantity = Assert.Single(result.Quantities.Where(row => row.DefinitionIdentity == "shared-def"));
            Assert.Equal("root", quantity.ParentRuntimeId);
            Assert.Equal(2, quantity.Quantity);
            Assert.Equal(BomDiagnosticQuantityStatus.Verified, quantity.Status);
            Assert.Equal(1, result.RepeatedDefinitionCount);
        }

        [Fact]
        public void Analyze_SeparatesSameDefinitionUnderDifferentParents()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("left", "Assembly", "left-def", Node("left-part", "Part", "shared-def")),
                Node("right", "Assembly", "right-def", Node("right-part", "Part", "shared-def")));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(2, result.Quantities.Count(row => row.DefinitionIdentity == "shared-def"));
            Assert.Contains(result.Quantities, row => row.ParentRuntimeId == "left" && row.Quantity == 1);
            Assert.Contains(result.Quantities, row => row.ParentRuntimeId == "right" && row.Quantity == 1);
        }

        [Fact]
        public void Analyze_DoesNotInventQuantityWhenDefinitionIdentityIsMissing()
        {
            var root = Node("root", "Assembly", "root-def", Node("part", "Part", null));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(BomDiagnosticQuantityStatus.IdentityUnavailable, result.QuantityStatus);
            Assert.Empty(result.Quantities);
            Assert.DoesNotContain(result.DepthFirstNodes, node => node.Quantity == 1);
        }

        [Fact]
        public void Analyze_NullRootProducesIdentityUnavailableWarning()
        {
            var result = BomDiagnosticTreeAnalyzer.Analyze(null);

            Assert.Equal(BomDiagnosticQuantityStatus.IdentityUnavailable, result.QuantityStatus);
            Assert.Contains(result.Warnings, warning => warning.Contains("Root node is missing"));
        }

        [Fact]
        public void Analyze_PreservesOptionalIdentityAndStateFields()
        {
            var source = Node("runtime", "Part", "definition");
            source.PersistentIdCandidate = "persistent-candidate";
            source.OccurrenceIdentityCandidate = "occurrence-candidate";
            source.ExternalFilePath = @"D:\CAD\part.ics";
            source.IsExternal = true;
            source.IsSuppressed = false;
            source.IsVisible = true;
            source.IncludedInBom = true;
            source.CustomPropertyCount = 3;

            var node = Assert.Single(BomDiagnosticTreeAnalyzer.Analyze(source).DepthFirstNodes);

            Assert.Equal("persistent-candidate", node.PersistentIdCandidate);
            Assert.Equal("occurrence-candidate", node.OccurrenceIdentityCandidate);
            Assert.Equal(@"D:\CAD\part.ics", node.ExternalFilePath);
            Assert.Equal(3, node.CustomPropertyCount);
            Assert.True(node.IsExternal);
            Assert.True(node.IsVisible);
            Assert.True(node.IncludedInBom);
        }

        [Fact]
        public void Analyze_IsDeterministicForTheSameProviderOrder()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("b", "Part", "b-def"), Node("a", "Part", "a-def"));

            var first = BomDiagnosticTreeAnalyzer.Analyze(root);
            var second = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(first.DepthFirstNodes.Select(node => node.RuntimeId),
                second.DepthFirstNodes.Select(node => node.RuntimeId));
        }

        [Fact]
        public void Analyze_DoesNotEmitPartialQuantityRowsWhenAnySiblingIdentityIsMissing()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("known", "Part", "known-def"), Node("unknown", "Part", null));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(BomDiagnosticQuantityStatus.IdentityUnavailable, result.QuantityStatus);
            Assert.Empty(result.Quantities);
        }

        [Fact]
        public void Sanitizer_ExcludesRawNamesAndAbsolutePaths()
        {
            var secretPart = Node("secret-part", "Part", "part-definition");
            secretPart.ExternalFilePath = @"C:\Private\Study\secret-part.ics";
            secretPart.DisplayName = "Proprietary Scene Name";
            var root = Node("runtime", "Assembly", "definition", secretPart);
            root.DisplayName = "Proprietary Assembly";
            var snapshot = BomDiagnosticTreeAnalyzer.Analyze(root);

            var evidence = BomDiagnosticSanitizer.CreateAggregate(snapshot);
            var json = evidence.ToJson();

            Assert.DoesNotContain("Proprietary", json);
            Assert.DoesNotContain("secret-part", json);
            Assert.DoesNotContain(@"C:\Private", json);
            Assert.Contains("AssemblyCount", json);
        }

        [Fact]
        public void DiagnosticOutput_UsesCreateNewAndCannotOverwriteExistingReport()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bom-diagnostic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var snapshot = BomDiagnosticTreeAnalyzer.Analyze(Node("root", "Assembly", "root-def"));
                var path = BomDiagnosticOutput.WriteRawSnapshot(snapshot, folder, "study", TestContext());

                Assert.True(File.Exists(path));
                Assert.Throws<IOException>(() => BomDiagnosticOutput.WriteRawSnapshot(snapshot, folder, "study", TestContext()));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void DiagnosticOutput_RequiresAnExplicitExistingFolder()
        {
            var analysis = BomDiagnosticTreeAnalyzer.Analyze(Node("root", "Assembly", "root-def"));

            Assert.Throws<ArgumentException>(() => BomDiagnosticOutput.WriteRawSnapshot(analysis, null, "study", TestContext()));
            Assert.Throws<DirectoryNotFoundException>(() => BomDiagnosticOutput.WriteRawSnapshot(
                analysis, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "study", TestContext()));
        }

        private static BomDiagnosticOutputContext TestContext(string protectedRoot = null)
        {
            return new BomDiagnosticOutputContext
            {
                RepositoryRoot = protectedRoot ?? Path.Combine(Path.GetTempPath(), "bom-test-repo-" + Guid.NewGuid().ToString("N")),
                StudyDirectory = Path.Combine(Path.GetTempPath(), "bom-test-study-" + Guid.NewGuid().ToString("N")),
                ApplicationDataDirectory = Path.Combine(Path.GetTempPath(), "bom-test-appdata-" + Guid.NewGuid().ToString("N"))
            };
        }

        [Fact]
        public void Sanitizer_EmitsAggregateFieldsWithoutRawNodeFields()
        {
            var source = Node("runtime", "Part", "definition");
            source.DisplayName = "Private CAD Name";
            source.ExternalFilePath = @"D:\Private\part.ics";

            var json = BomDiagnosticSanitizer.CreateAggregate(BomDiagnosticTreeAnalyzer.Analyze(source)).ToJson();

            Assert.Contains("TotalNodes", json);
            Assert.DoesNotContain("DepthFirstNodes", json);
            Assert.DoesNotContain("DisplayName", json);
            Assert.DoesNotContain("ExternalFilePath", json);
            Assert.DoesNotContain("Private CAD Name", json);
        }

        private static BomDiagnosticSourceNode Node(string runtimeId, string kind, string definitionIdentity,
            params BomDiagnosticSourceNode[] children)
        {
            var node = new BomDiagnosticSourceNode
            {
                RuntimeId = runtimeId,
                NodeKind = kind,
                DefinitionIdentityCandidate = definitionIdentity,
                DisplayName = runtimeId,
                Children = children == null ? null : children.ToList()
            };
            return node;
        }
    }
}
