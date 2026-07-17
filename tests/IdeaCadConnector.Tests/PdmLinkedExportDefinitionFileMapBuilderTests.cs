using System.Collections.Generic;
using System.Linq;
using IdeaCadConnector.IronCAD.NormalizeExport;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmLinkedExportDefinitionFileMapBuilderTests
    {
        [Fact]
        public void Build_GroupsSourceNodesBySharedElementId()
        {
            var plan = CreatePlan();
            var sourceNodeA = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sourceNodeB = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].SourceNode = sourceNodeA;
            AddPart(plan, "0/1", "child2", sourceNodeB);

            var elementIds = new Dictionary<PdmSourceNode, ElementId>
            {
                { sourceNodeA, new ElementId(1) },
                { sourceNodeB, new ElementId(1) }
            };

            var builder = new IronCadDefinitionFileMapBuilder();
            var map = builder.Build(elementIds, plan);

            Assert.Equal(2, map.Count);
            Assert.Equal(map[sourceNodeA], map[sourceNodeB]);
        }

        [Fact]
        public void Build_DifferentElementId_ProducesDifferentFiles()
        {
            var plan = CreatePlan();
            var sourceNodeA = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sourceNodeB = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A02" };
            plan.Parts[0].SourceNode = sourceNodeA;
            AddPart(plan, "0/1", "child2", sourceNodeB);

            var elementIds = new Dictionary<PdmSourceNode, ElementId>
            {
                { sourceNodeA, new ElementId(1) },
                { sourceNodeB, new ElementId(2) }
            };

            var builder = new IronCadDefinitionFileMapBuilder();
            var map = builder.Build(elementIds, plan);

            Assert.Equal(2, map.Count);
            Assert.NotEqual(map[sourceNodeA], map[sourceNodeB]);
        }

        [Fact]
        public void Build_MissingSourceNodeInPlan_SkipsEntry()
        {
            var plan = CreatePlan();
            var sourceNodeInMap = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sourceNodeNotInPlan = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "ORPHAN" };
            plan.Parts[0].SourceNode = sourceNodeInMap;

            var elementIds = new Dictionary<PdmSourceNode, ElementId>
            {
                { sourceNodeInMap, new ElementId(1) },
                { sourceNodeNotInPlan, new ElementId(2) }
            };

            var builder = new IronCadDefinitionFileMapBuilder();
            var map = builder.Build(elementIds, plan);

            Assert.Single(map);
            Assert.True(map.ContainsKey(sourceNodeInMap));
        }

        [Fact]
        public void Build_PlanItemWithNullSourceNode_SkipsEntry()
        {
            var plan = CreatePlan();
            plan.Parts[0].SourceNode = null;

            var elementIds = new Dictionary<PdmSourceNode, ElementId>();

            var builder = new IronCadDefinitionFileMapBuilder();
            var map = builder.Build(elementIds, plan);

            Assert.Empty(map);
        }

        [Fact]
        public void Build_AmbiguousItemCodes_StillGroupsByElementId()
        {
            var plan = CreatePlan();
            var sourceNode = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sameElement = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].SourceNode = sourceNode;
            AddPart(plan, "0/1", "child2", sameElement);

            var elementIds = new Dictionary<PdmSourceNode, ElementId>
            {
                { sourceNode, new ElementId(1) },
                { sameElement, new ElementId(1) }
            };

            var builder = new IronCadDefinitionFileMapBuilder();
            var map = builder.Build(elementIds, plan);

            Assert.Equal(2, map.Count);
            Assert.Equal(map[sourceNode], map[sameElement]);
        }

        private static PdmNormalizationPlan CreatePlan()
        {
            var plan = new PdmNormalizationPlan { ProjectCode = "PDM-TEST", Revision = "A" };
            plan.Root = new PdmPlanItem
            {
                OccurrencePath = "0", NodeId = "root", ItemCode = "ROOT", ItemType = "ASM",
                DisplayName = "ROOT", SceneName = "ROOT", ProjectCode = "PDM-TEST", Revision = "A",
                SourceKind = PdmNodeKind.SceneRoot, CanonicalFileName = "root.ics"
            };
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/0", ParentNodeId = "root", NodeId = "child", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1,
                CanonicalFileName = "PDM-TEST__A01__CHILD.ics"
            });
            return plan;
        }

        private static void AddPart(PdmNormalizationPlan plan, string occurrencePath, string nodeId, PdmSourceNode sourceNode)
        {
            var code = sourceNode?.Name ?? "A01";
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = occurrencePath, ParentNodeId = "root", NodeId = nodeId, ItemCode = code,
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1, SourceNode = sourceNode,
                CanonicalFileName = $"PDM-TEST__{code}__CHILD.ics"
            });
        }
    }
}
