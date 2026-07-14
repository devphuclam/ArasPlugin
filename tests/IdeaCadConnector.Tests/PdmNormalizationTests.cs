using System.Linq;
using System.IO;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmNormalizationTests
    {
        [Fact]
        public void NormalizeProjectCode_UsesCanonicalUppercaseSlug()
        {
            Assert.Equal("PDM-STUDYCASE", PdmNameNormalizer.DeriveProjectCodeFromRootFileName("PDM_StudyCase_260713-1.ics"));
        }

        [Fact]
        public void ParseNodeName_PreservesLeadingBusinessCode()
        {
            var parsed = PdmNameNormalizer.ParseNodeName("A01_MainBodyBase");

            Assert.Equal("A01", parsed.ItemCode);
            Assert.Equal("MAIN-BODY-BASE", parsed.DisplayName);
        }

        [Fact]
        public void NormalizeDisplayName_ConvertsCamelCaseAndSeparators()
        {
            Assert.Equal("FOOT-SWITCH-TOP", PdmNameNormalizer.NormalizeDisplayName("FootSwitch_Top"));
        }

        [Fact]
        public void Plan_UsesDeterministicDepthFirstCodesAndExcludesSceneRoot()
        {
            var source = new PdmSourceNode
            {
                Kind = PdmNodeKind.SceneRoot,
                Name = "PDM_StudyCase_260713-1",
                Children = new[]
                {
                    new PdmSourceNode { Kind = PdmNodeKind.Assembly, Name = "Assembly1" },
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part25" }
                }
            };

            var plan = new PdmNormalizationPlanner().CreatePlan("PDM-STUDYCASE", "A", source);

            Assert.Equal("PDM-STUDYCASE", plan.ProjectCode);
            Assert.Equal(1, plan.Assemblies.Count);
            Assert.Equal(1, plan.Parts.Count);
            Assert.Equal("ASM-001", plan.Assemblies.Single().ItemCode);
            Assert.Equal("PRT-001", plan.Parts.Single().ItemCode);
            Assert.Equal("PDM-STUDYCASE__ASM__ASM-001__ASSEMBLY-001.ics", plan.Assemblies.Single().CanonicalFileName);
        }

        [Fact]
        public void Plan_PrefersExistingPdmItemCodeAndNodeId()
        {
            var source = new PdmSourceNode
            {
                Kind = PdmNodeKind.Part,
                Name = "GenericPart",
                Properties = new PdmSourceProperties
                {
                    NodeId = "existing-node-id",
                    ItemCode = "C03"
                }
            };

            var item = new PdmNormalizationPlanner().CreatePlan("PDM-STUDY", "A", source).Parts.Single();

            Assert.Equal("C03", item.ItemCode);
            Assert.Equal("existing-node-id", item.NodeId);
        }

        [Fact]
        public void Plan_ReportsDuplicateItemCodesAndFilenames()
        {
            var source = new PdmSourceNode
            {
                Kind = PdmNodeKind.SceneRoot,
                Name = "PDM-STUDY",
                Children = new[]
                {
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01_First" },
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01_First" }
                }
            };

            var plan = new PdmNormalizationPlanner().CreatePlan("PDM-STUDY", "A", source);

            Assert.Contains(PdmPlanWarning.DuplicateItemCode, plan.Warnings);
            Assert.Contains(PdmPlanWarning.DuplicateFileName, plan.Warnings);
        }

        [Fact]
        public void ManifestWriter_SerializesRootItemsAndBomEdges()
        {
            var manifest = new PdmPackageManifest
            {
                ProjectCode = "PDM-STUDY",
                Revision = "A",
                RootNodeId = "root",
                RootItemCode = "ROOT",
                RootFile = "cad/root.ics",
                Items = new[]
                {
                    new PdmManifestItem
                    {
                        NodeId = "child",
                        ItemCode = "A01",
                        ItemType = "PRT",
                        DisplayName = "MAIN-BODY",
                        SceneName = "A01_MAIN-BODY",
                        FileName = "cad/child.ics",
                        Revision = "A"
                    }
                },
                Bom = new[]
                {
                    new PdmManifestBomEdge
                    {
                        ParentNodeId = "root",
                        ChildNodeId = "child",
                        FindNumber = 10,
                        Quantity = 1,
                        QuantityStatus = "OccurrenceBased"
                    }
                }
            };

            var json = new PdmPackageManifestWriter().Serialize(manifest);

            Assert.Contains("\"schemaVersion\": 1", json);
            Assert.Contains("\"rootFile\": \"cad/root.ics\"", json);
            Assert.Contains("\"quantityStatus\": \"OccurrenceBased\"", json);
        }

        [Fact]
        public void PackageValidator_ReportsMissingManifestFilesAndUnknownBomNodes()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-validate-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "cad"));
            File.WriteAllText(Path.Combine(root, "cad", "root.ics"), "root");
            var manifest = new PdmPackageManifest
            {
                RootFile = "cad/root.ics",
                Items = new[] { new PdmManifestItem { NodeId = "child", FileName = "cad/missing.ics" } },
                Bom = new[] { new PdmManifestBomEdge { ParentNodeId = "root", ChildNodeId = "unknown" } }
            };

            var result = new PdmPackageValidator().Validate(root, manifest);

            Assert.Contains(PdmPackageValidationIssue.MissingFile, result.Issues);
            Assert.Contains(PdmPackageValidationIssue.UnknownBomNode, result.Issues);
        }
    }
}
