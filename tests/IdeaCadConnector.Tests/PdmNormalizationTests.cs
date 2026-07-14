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
                BomV2 = new[]
                {
                    new PdmManifestBomV2
                    {
                        ParentOccurrenceId = "occ-0",
                        ChildDefinitionId = "def-child",
                        Quantity = 1,
                        QuantityStatus = "IdentityUnavailable"
                    }
                }
            };

            var json = new PdmPackageManifestWriter().Serialize(manifest);

            Assert.Contains("\"schemaVersion\": 2", json);
            Assert.Contains("\"rootFile\": \"cad/root.ics\"", json);
            Assert.Contains("\"quantityStatus\": \"IdentityUnavailable\"", json);
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
                BomV2 = new[] { new PdmManifestBomV2 { ParentOccurrenceId = "occ-root", ChildDefinitionId = "unknown" } }
            };

            var result = new PdmPackageValidator().Validate(root, manifest);

            Assert.Contains(PdmPackageValidationIssue.MissingFile, result.Issues);
            Assert.Contains(PdmPackageValidationIssue.UnknownBomNode, result.Issues);
        }

        [Fact]
        public void FeatureFlag_IsDisabledUnlessExplicitlyEnabled()
        {
            Assert.False(PdmFeatureFlags.IsNormalizeExportEnabled(null));
            Assert.False(PdmFeatureFlags.IsNormalizeExportEnabled("false"));
            Assert.True(PdmFeatureFlags.IsNormalizeExportEnabled("true"));
        }

        [Fact]
        public void FinalPlan_RebuildsAllDerivedValuesFromDialogEdits()
        {
            var source = new PdmSourceNode
            {
                Kind = PdmNodeKind.SceneRoot,
                Name = "Root",
                Children = new[] { new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "A01_OldName" } }
            };
            var initial = new PdmNormalizationPlanner().CreatePlan("PDM-OLD", "A", source);
            var item = initial.Parts.Single();
            var result = new NormalizeExportDialogResult
            {
                ProjectCode = "PDM-NEW",
                Revision = "B",
                OutputFolder = "C:\\export",
                Edits = new[] { new NormalizeExportEdit { SourceNode = item.SourceNode, NodeId = item.NodeId, ItemCode = "B02", DisplayName = "NEW-NAME" } }
            };

            var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(source, result);

            Assert.Equal("PDM-NEW", finalPlan.ProjectCode);
            Assert.Equal("B", finalPlan.Revision);
            Assert.Equal("B02_NEW-NAME", finalPlan.Parts.Single().SceneName);
            Assert.Equal("PDM-NEW__PRT__B02__NEW-NAME.ics", finalPlan.Parts.Single().CanonicalFileName);
        }

        [Fact]
        public void Preflight_BlocksGenericNamesAndDuplicateIdsBeforeWrite()
        {
            var source = new PdmSourceNode
            {
                Kind = PdmNodeKind.SceneRoot,
                Name = "Root",
                Children = new[]
                {
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part1", Properties = new PdmSourceProperties { NodeId = "same" } },
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part2", Properties = new PdmSourceProperties { NodeId = "same" } }
                }
            };
            var plan = new PdmNormalizationPlanner().CreatePlan("PDM-NEW", "A", source);

            var issues = new PdmNormalizationPreflightValidator().Validate(plan, "C:\\export");

            Assert.Contains(PdmPreflightIssue.GenericNameNotConfirmed, issues);
            Assert.Contains(PdmPreflightIssue.DuplicateNodeId, issues);
        }

        [Fact]
        public void Planner_StopsCyclesAndExcessiveDepth()
        {
            var source = new PdmSourceNode { Kind = PdmNodeKind.SceneRoot, Name = "Root" };
            source.Children = new[] { source };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new PdmNormalizationPlanner().CreatePlan("PDM-NEW", "A", source, new PdmNormalizationLimits { MaxDepth = 4, MaxNodeCount = 10 }));

            Assert.Equal("PDM_TRAVERSAL_CYCLE", exception.Message);
        }

        [Fact]
        public void DuplicateSiblingNames_GetUniqueOccurrencePaths_AndEditedNodeIdIsStable()
        {
            var root = new PdmSourceNode { Kind = PdmNodeKind.SceneRoot, Name = "Root", Children = new[] {
                new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part1" },
                new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part1" } } };
            var initial = new PdmNormalizationPlanner().CreatePlan("PDM-NEW", "A", root);
            var edit = new NormalizeExportEdit { SourceNode = initial.Parts[0].SourceNode, OccurrencePath = initial.Parts[0].OccurrencePath,
                NodeId = initial.Parts[0].NodeId, ItemCode = "a 01", DisplayName = "MainBody", GenericNameConfirmed = true };
            var finalPlan = new PdmNormalizationPlanner().CreateFinalPlan(root, new NormalizeExportDialogResult {
                ProjectCode = "PDM-NEW", Revision = "A", OutputFolder = Path.GetTempPath(), Edits = new[] { edit } });
            Assert.Equal("0/0", finalPlan.Parts[0].OccurrencePath);
            Assert.Equal("0/1", finalPlan.Parts[1].OccurrencePath);
            Assert.Equal(initial.Parts[0].NodeId, finalPlan.Parts[0].NodeId);
            Assert.Equal("A-01", finalPlan.Parts[0].ItemCode);
            Assert.Contains("A-01", finalPlan.Parts[0].SceneName);
        }

        [Fact]
        public void DescriptiveNameIsNotGenericPlaceholder_ButUnconfirmedPartIsBlocked()
        {
            var descriptive = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "MainBodyBase" };
            Assert.False(new PdmNormalizationPlanner().CreatePlan("PDM-NEW", "A", descriptive).Parts.Single().SourceWasGeneric);
            var generic = new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "Part1" };
            var plan = new PdmNormalizationPlanner().CreatePlan("PDM-NEW", "A", generic);
            Assert.Contains(PdmPreflightIssue.GenericNameNotConfirmed,
                new PdmNormalizationPreflightValidator().Validate(plan, Path.GetTempPath()));
        }

        [Fact]
        public void SourceFingerprintDetectsByteChange()
        {
            var file = Path.Combine(Path.GetTempPath(), "pdm-source-" + System.Guid.NewGuid().ToString("N") + ".ics");
            File.WriteAllText(file, "one");
            var fingerprint = PdmSourceIntegrity.Capture(file);
            File.WriteAllText(file, "two");
            Assert.False(PdmSourceIntegrity.Matches(fingerprint));
            File.Delete(file);
        }

        [Fact]
        public void OutputInsideSourceRootIsRejected()
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), "pdm-source-root-" + System.Guid.NewGuid().ToString("N"));
            var output = Path.Combine(sourceRoot, "out");
            Directory.CreateDirectory(output);
            var issues = new PdmOutputSafetyValidator().Validate(output, Path.Combine(sourceRoot, "root.ics"), null);
            Assert.Contains(PdmOutputSafetyIssue.SourceOverlap, issues);
            Directory.Delete(sourceRoot, true);
        }
    }
}
