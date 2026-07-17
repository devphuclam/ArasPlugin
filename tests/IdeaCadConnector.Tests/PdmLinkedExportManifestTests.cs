using System;
using System.Collections.Generic;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;
using Newtonsoft.Json;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmLinkedExportManifestTests
    {
        [Fact]
        public void OccurrenceDefinitionFile_SerializesAsDefinitionFileJsonKey()
        {
            var json = new PdmPackageManifestWriter().Serialize(new PdmPackageManifest
            {
                SchemaVersion = 2,
                Occurrences = new[] { new PdmManifestOccurrence
                {
                    OccurrenceId = "occ-0", OccurrencePath = "0", DefinitionId = "def-0",
                    DefinitionFile = "MYASM__P01__Bracket.ics"
                }}
            });
            Assert.Contains("\"definitionFile\":", json);
            Assert.Contains("MYASM__P01__Bracket.ics", json);
        }

        [Fact]
        public void OccurrenceDefinitionFile_DeserializesCorrectly()
        {
            var json = @"{""occurrences"":[{""occurrenceId"":""occ-0"",""occurrencePath"":""0"",""definitionId"":""def-0"",""definitionFile"":""MYASM__P01__Bracket.ics""}]}";
            var manifest = JsonConvert.DeserializeObject<PdmPackageManifest>(json);
            Assert.Equal("MYASM__P01__Bracket.ics", manifest.Occurrences.Single().DefinitionFile);
        }

        [Fact]
        public void FactoryCreate_WithMap_SetsDefinitionFileOnOccurrences()
        {
            var plan = CreatePlan();
            var sourceNode = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].SourceNode = sourceNode;
            var map = new Dictionary<PdmSourceNode, string> { { sourceNode, "MYASM__A01__CHILD.ics" } };

            var manifest = PdmManifestV2Factory.Create(plan, map);

            var childOcc = manifest.Occurrences.Single(o => o.OccurrencePath == "0/0");
            Assert.Equal("MYASM__A01__CHILD.ics", childOcc.DefinitionFile);
        }

        [Fact]
        public void FactoryCreate_WithMap_DeduplicatesDefinitions()
        {
            var plan = CreatePlan();
            var sourceNode1 = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sourceNode2 = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].SourceNode = sourceNode1;
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/1", ParentNodeId = "root", NodeId = "child2", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1, SourceNode = sourceNode2
            });
            var map = new Dictionary<PdmSourceNode, string>
            {
                { sourceNode1, "MYASM__A01__CHILD.ics" },
                { sourceNode2, "MYASM__A01__CHILD.ics" }
            };

            var manifest = PdmManifestV2Factory.Create(plan, map);

            Assert.Single(manifest.Definitions.Where(d => d.FileName.Contains("CHILD")));
            var childOccs = manifest.Occurrences.Where(o => o.OccurrencePath != "0").ToArray();
            Assert.Equal(2, childOccs.Length);
            Assert.Equal(childOccs[0].DefinitionId, childOccs[1].DefinitionId);
        }

        [Fact]
        public void FactoryCreate_WithBuilderCadPath_DoesNotDuplicateCadDirectory()
        {
            var plan = CreatePlan();
            var sourceNode = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].SourceNode = sourceNode;
            var map = new Dictionary<PdmSourceNode, string>
            {
                { sourceNode, "cad/MYASM__A01__CHILD.ics" }
            };

            var manifest = PdmManifestV2Factory.Create(plan, map);

            Assert.Equal("cad/MYASM__A01__CHILD.ics",
                manifest.Definitions.Single(d => d.ItemCode == "A01").FileName);
        }

        [Fact]
        public void FactoryCreate_WithNullMap_DoesNotSetDefinitionFile()
        {
            var plan = CreatePlan();
            var manifest = PdmManifestV2Factory.Create(plan);
            Assert.All(manifest.Occurrences, occ => Assert.Null(occ.DefinitionFile));
        }

        [Fact]
        public void FactoryCreate_SourceNodeMissingFromMap_ReportsWarning()
        {
            var plan = CreatePlan();
            plan.Parts[0].SourceNode = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var map = new Dictionary<PdmSourceNode, string>();

            var manifest = PdmManifestV2Factory.Create(plan, map);

            Assert.Contains(manifest.Warnings, w => w.Contains("MISSING_DEFINITION_MAP_ENTRY") || w.Contains("missing"));
        }

        [Fact]
        public void FactoryCreate_ConflictingMetadata_ReportsError()
        {
            var plan = CreatePlan();
            var sourceNode1 = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            var sourceNode2 = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts[0].ItemCode = "A01";
            plan.Parts[0].SourceNode = sourceNode1;
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/1", ParentNodeId = "root", NodeId = "child2", ItemCode = "A02",
                ItemType = "PRT", DisplayName = "CHILD2", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1, SourceNode = sourceNode2
            });
            var map = new Dictionary<PdmSourceNode, string>
            {
                { sourceNode1, "MYASM__A01__CHILD.ics" },
                { sourceNode2, "MYASM__A01__CHILD.ics" }
            };

            var manifest = PdmManifestV2Factory.Create(plan, map);
            var defsForFile = manifest.Definitions.Where(d => d.FileName.Contains("CHILD")).ToArray();

            Assert.Single(defsForFile);
        }

        [Fact]
        public void FactoryCreate_DeduplicatedManifest_PassesCrossReferenceChecks()
        {
            var plan = new PdmNormalizationPlan { ProjectCode = "PDM-TEST", Revision = "A" };
            plan.Root = new PdmPlanItem
            {
                OccurrencePath = "0", NodeId = "root", ItemCode = "ROOT", ItemType = "ASM",
                DisplayName = "ROOT", SceneName = "ROOT", ProjectCode = "PDM-TEST", Revision = "A",
                SourceKind = PdmNodeKind.SceneRoot, SourceNode = new PdmSourceNode { Kind = PdmNodeKind.SceneRoot, Name = "ROOT" }
            };
            var sharedSource = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01" };
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/0", ParentNodeId = "root", NodeId = "child1", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1, SourceNode = sharedSource
            });
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/1", ParentNodeId = "root", NodeId = "child2", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1, SourceNode = sharedSource
            });

            var map = new Dictionary<PdmSourceNode, string>
            {
                { plan.Root.SourceNode, "PDM-TEST__ROOT__ROOT.ics" },
                { sharedSource, "PDM-TEST__A01__CHILD.ics" }
            };

            var manifest = PdmManifestV2Factory.Create(plan, map);

            var defIds = manifest.Definitions.Select(d => d.DefinitionId).ToHashSet();
            foreach (var occ in manifest.Occurrences)
                Assert.Contains(occ.DefinitionId, defIds);

            foreach (var bom in manifest.BomV2)
                Assert.Contains(bom.ChildDefinitionId, defIds);
        }

        private static PdmNormalizationPlan CreatePlan()
        {
            var plan = new PdmNormalizationPlan { ProjectCode = "PDM-TEST", Revision = "A" };
            plan.Root = new PdmPlanItem
            {
                OccurrencePath = "0", NodeId = "root", ItemCode = "ROOT", ItemType = "ASM",
                DisplayName = "ROOT", SceneName = "ROOT", ProjectCode = "PDM-TEST", Revision = "A",
                SourceKind = PdmNodeKind.SceneRoot
            };
            plan.Parts.Add(new PdmPlanItem
            {
                OccurrencePath = "0/0", ParentNodeId = "root", NodeId = "child", ItemCode = "A01",
                ItemType = "PRT", DisplayName = "CHILD", SceneName = "ROOT", ProjectCode = "PDM-TEST",
                Revision = "A", SourceKind = PdmNodeKind.Part, Depth = 1
            });
            return plan;
        }
    }
}
