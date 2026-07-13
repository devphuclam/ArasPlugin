using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.IronCAD.BomDiagnostic;
using IdeaCadConnector.Workspace.BomDiagnostic;
using Newtonsoft.Json;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class BomDiagnosticFixRegressionTests
    {
        [Theory]
        [InlineData("Z_ELEMENT_PART", "Part")]
        [InlineData("Z_ELEMENT_ASSEMBLY", "Assembly")]
        [InlineData("Z_ELEMENT_BREP", "TechnicalOrUnknown")]
        [InlineData("Z_ELEMENT_UNKNOWN_VALUE", "TechnicalOrUnknown")]
        public void IcapiNodeKindMapper_MapsProviderEnumShapes(string rawKind, string expected)
        {
            Assert.Equal(expected, IronCadBomDiagnosticNodeKindMapper.Map(rawKind));
        }

        [Fact]
        public void Analyzer_MarksExplicitlyAmbiguousDefinitionIdentity()
        {
            var root = Node("root", "Assembly", "root-def",
                AmbiguousNode("ambiguous", "Part", "shared-def"));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(BomDiagnosticQuantityStatus.AmbiguousDefinition, result.QuantityStatus);
            Assert.Contains(result.Quantities, row => row.Status == BomDiagnosticQuantityStatus.AmbiguousDefinition);
        }

        [Fact]
        public void Analyzer_DoesNotEmitQuantityRowsForParentWithMissingIdentity()
        {
            var root = Node("root", "Assembly", "root-def",
                Node("known", "Part", "known-def"), Node("unknown", "Part", null));

            var result = BomDiagnosticTreeAnalyzer.Analyze(root);

            Assert.Equal(BomDiagnosticQuantityStatus.IdentityUnavailable, result.QuantityStatus);
            Assert.Empty(result.Quantities);
            Assert.DoesNotContain(result.DepthFirstNodes, node => node.Quantity == 1);
        }

        [Fact]
        public void Analyzer_DoesNotGroupIndependentDefinitionsFromOccurrenceIdentity()
        {
            var first = Node("one", "Part", "definition-a");
            first.OccurrenceIdentityCandidate = "occurrence-shared";
            var second = Node("two", "Part", "definition-b");
            second.OccurrenceIdentityCandidate = "occurrence-shared";
            var result = BomDiagnosticTreeAnalyzer.Analyze(Node("root", "Assembly", "root-def", first, second));

            Assert.Equal(2, result.Quantities.Count);
            Assert.All(result.Quantities, row => Assert.Equal(1, row.Quantity));
            Assert.Equal(0, result.RepeatedDefinitionCount);
        }

        [Fact]
        public void DiagnosticOutput_IsValidJsonAndKeepsWarningInsideObject()
        {
            var folder = CreateFolder();
            try
            {
                var snapshot = new BomDiagnosticSnapshot
                {
                    DocumentName = "study",
                    Analysis = BomDiagnosticTreeAnalyzer.Analyze(Node("root", "Assembly", "root-def"))
                };

                var path = BomDiagnosticOutput.WriteRawSnapshot(snapshot, folder, "study");
                var parsed = JsonConvert.DeserializeObject<BomDiagnosticSnapshot>(File.ReadAllText(path));

                Assert.NotNull(parsed);
                Assert.Equal(snapshot.LocalReportWarning, parsed.LocalReportWarning);
                Assert.Throws<IOException>(() => BomDiagnosticOutput.WriteRawSnapshot(snapshot, folder, "study"));
            }
            finally { DeleteFolder(folder); }
        }

        [Fact]
        public void Sanitizer_ConvertsRawWarningsToStableCategories()
        {
            var analysis = BomDiagnosticTreeAnalyzer.Analyze(Node("runtime", "Part", "definition"));
            var fakeUsername = "TD-" + "999";
            var fakePath = "C:" + "\\Users\\" + fakeUsername + "\\Private\\secret.ics";
            analysis.Warnings.Add("Model link path unavailable for node 'secret': " + fakePath);
            analysis.Warnings.Add("Custom properties unavailable for node 'secret': machine=" + fakeUsername);

            var json = BomDiagnosticSanitizer.CreateAggregate(analysis).ToJson();

            Assert.Contains("MODEL_LINK_READ_FAILED", json);
            Assert.Contains("CUSTOM_PROPERTY_READ_FAILED", json);
            Assert.DoesNotContain(fakeUsername, json);
            Assert.DoesNotContain("secret.ics", json);
        }

        [Fact]
        public void OutputPathPolicy_AcceptsExternalTempAndRejectsProtectedRoots()
        {
            var root = CreateFolder();
            var repository = Path.Combine(root, "repo");
            var study = Path.Combine(root, "study");
            var appData = Path.Combine(root, "appdata");
            Directory.CreateDirectory(Path.Combine(repository, ".git"));
            Directory.CreateDirectory(Path.Combine(repository, "src", "Feature"));
            Directory.CreateDirectory(study);
            Directory.CreateDirectory(appData);
            var safe = Path.Combine(root, "external");
            Directory.CreateDirectory(safe);
            try
            {
                Assert.Equal(Path.GetFullPath(safe), BomDiagnosticOutputPathPolicy.Validate(
                    safe, repository, study, appData));
                Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutputPathPolicy.Validate(
                    repository, repository, study, appData));
                Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutputPathPolicy.Validate(
                    Path.Combine(repository, ".git"), repository, study, appData));
                Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutputPathPolicy.Validate(
                    Path.Combine(repository, "src", "Feature"), repository, study, appData));
                Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutputPathPolicy.Validate(
                    study, repository, study, appData));
                Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutputPathPolicy.Validate(
                    appData, repository, study, appData));
            }
            finally { DeleteFolder(root); }
        }

        [Fact]
        public void DiagnosticOutput_RejectsRepositoryFolderByDefault()
        {
            var analysis = BomDiagnosticTreeAnalyzer.Analyze(Node("root", "Assembly", "root-def"));
            Assert.Throws<InvalidOperationException>(() => BomDiagnosticOutput.WriteRawSnapshot(
                analysis, Directory.GetCurrentDirectory(), "unsafe-repository-report"));
        }

        [Fact]
        public void TraversalGuard_DetectsCyclesAndFiniteLimitsWithoutCollapsingRepeatedOccurrences()
        {
            var guard = new BomDiagnosticTraversalGuard(2, 3);
            var root = new object();
            var repeatedDefinitionOccurrence = new object();

            Assert.Equal(BomDiagnosticTraversalDecision.Entered, guard.TryEnter(root, 0));
            Assert.Equal(BomDiagnosticTraversalDecision.Entered, guard.TryEnter(repeatedDefinitionOccurrence, 1));
            guard.Exit(repeatedDefinitionOccurrence);
            Assert.Equal(BomDiagnosticTraversalDecision.Entered, guard.TryEnter(repeatedDefinitionOccurrence, 1));
            Assert.Equal(BomDiagnosticTraversalDecision.Cycle, guard.TryEnter(root, 1));
            guard.Exit(repeatedDefinitionOccurrence);
            guard.Exit(root);
            Assert.Equal(BomDiagnosticTraversalDecision.MaxDepth, guard.TryEnter(new object(), 3));
            Assert.Equal(BomDiagnosticTraversalDecision.MaxNodes, guard.TryEnter(new object(), 2));
        }

        private static BomDiagnosticSourceNode Node(string runtimeId, string kind, string definition,
            params BomDiagnosticSourceNode[] children)
        {
            return new BomDiagnosticSourceNode
            {
                RuntimeId = runtimeId,
                NodeKind = kind,
                DefinitionIdentityCandidate = definition,
                DisplayName = runtimeId,
                Children = children == null ? null : children.ToList()
            };
        }

        private static BomDiagnosticSourceNode AmbiguousNode(string runtimeId, string kind, string definition)
        {
            var node = Node(runtimeId, kind, definition);
            node.DefinitionIdentityIsAmbiguous = true;
            return node;
        }

        private static string CreateFolder()
        {
            var path = Path.Combine(Path.GetTempPath(), "bom-fix-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteFolder(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
