using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmNormalizeExportSafetyTests
    {
        [Fact]
        public void RelativeLink_ResolvesAgainstDocumentDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-link-" + Guid.NewGuid().ToString("N"));
            var expected = Path.Combine(root, "parts", "child.ics");
            Assert.Equal(Path.GetFullPath(expected), PdmExternalReferencePolicy.ResolveLinkTarget("parts\\child.ics", root));
        }

        [Fact]
        public void AbsoluteLink_RemainsAbsolute()
        {
            var expected = Path.Combine(Path.GetTempPath(), "child.ics");
            Assert.Equal(Path.GetFullPath(expected), PdmExternalReferencePolicy.ResolveLinkTarget(expected, "C:\\ignored"));
        }

        [Fact]
        public void PackageValidator_RejectsOrphanIcsFile()
        {
            var package = CreateValidPackage(out var manifest);
            File.WriteAllText(Path.Combine(package, "cad", "orphan.ics"), "orphan");
            var result = new PdmPackageValidator().Validate(package, manifest);
            Assert.Contains(PdmPackageValidationIssue.OrphanFile, result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void PackageValidator_RejectsDuplicateDefinitionFileAndItemCode()
        {
            var package = CreateValidPackage(out var manifest);
            manifest.Definitions = manifest.Definitions.Concat(new[] { new PdmManifestDefinition
            {
                DefinitionId = "def-1", NodeId = Guid.NewGuid().ToString("D"), ItemCode = "ROOT",
                ItemType = "PRT", DisplayName = "SECOND", Revision = "A", FileName = "cad/root.ics"
            } }).ToArray();
            manifest.Occurrences = manifest.Occurrences.Concat(new[] { new PdmManifestOccurrence
            {
                OccurrenceId = "occ-1", OccurrencePath = "0/0", ParentOccurrenceId = "occ-0",
                DefinitionId = "def-1", FindNumber = 10
            } }).ToArray();
            var result = new PdmPackageValidator().Validate(package, manifest);
            Assert.Contains(PdmPackageValidationIssue.DuplicateFileName, result.Issues);
            Assert.Contains(PdmPackageValidationIssue.DuplicateItemCode, result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void PackageValidator_RejectsDuplicateCanonicalFileTargets()
        {
            var package = CreateValidPackage(out var manifest);
            var original = manifest.Definitions.Single();
            manifest.Definitions = new[] { original, new PdmManifestDefinition {
                DefinitionId = "def-1", NodeId = Guid.NewGuid().ToString("D"), ItemCode = "OTHER", ItemType = "PRT",
                DisplayName = "OTHER", Revision = "A", FileName = "cad\\root.ics" } };
            manifest.Occurrences = manifest.Occurrences.Concat(new[] { new PdmManifestOccurrence {
                OccurrenceId = "occ-1", OccurrencePath = "0/0", ParentOccurrenceId = "occ-0", DefinitionId = "def-1", FindNumber = 10 } }).ToArray();
            var result = new PdmPackageValidator().Validate(package, manifest);
            Assert.Contains(PdmPackageValidationIssue.DuplicateFileName, result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void PackageValidator_DoesNotDependOnLegacyProjection()
        {
            var package = CreateValidPackage(out var manifest);
            manifest.Items = new[] { new PdmManifestItem { NodeId = "bogus", ItemCode = "BOGUS", FileName = "cad/missing.ics" } };
            var result = new PdmPackageValidator().Validate(package, manifest);
            Assert.DoesNotContain(PdmPackageValidationIssue.MissingFile, result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void PendingValidationFailure_LeavesFinalAbsent()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-publish-" + Guid.NewGuid().ToString("N"));
            var staging = Path.Combine(root, "staging"); var pending = Path.Combine(root, "package.pending"); var final = Path.Combine(root, "package");
            Directory.CreateDirectory(staging); File.WriteAllText(Path.Combine(staging, "marker"), "x");
            var transaction = new PdmPackagePublicationTransaction(staging, pending, final);
            transaction.MoveToPending();
            transaction.RollbackPending();
            Assert.False(Directory.Exists(pending));
            Assert.False(Directory.Exists(final));
            Directory.Delete(root, true);
        }

        [Fact]
        public void Commit_RenamesPendingToFinalOnlyAfterPendingExists()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-publish-" + Guid.NewGuid().ToString("N"));
            var staging = Path.Combine(root, "staging"); var pending = Path.Combine(root, "package.pending"); var final = Path.Combine(root, "package");
            Directory.CreateDirectory(staging);
            var transaction = new PdmPackagePublicationTransaction(staging, pending, final);
            transaction.MoveToPending(); transaction.CommitPending();
            Assert.False(Directory.Exists(pending)); Assert.True(Directory.Exists(final));
            Directory.Delete(root, true);
        }

        [Fact]
        public void PublicationMoveFailure_UsesStablePackageCommitCode()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-publish-" + Guid.NewGuid().ToString("N"));
            var staging = Path.Combine(root, "staging"); Directory.CreateDirectory(staging);
            var transaction = new PdmPackagePublicationTransaction(staging,
                Path.Combine(root, "missing-parent", "pending"), Path.Combine(root, "final"));
            var error = Assert.Throws<PdmNormalizeExportException>(() => transaction.MoveToPending());
            Assert.Equal("PACKAGE_COMMIT_FAILED", error.Code);
            Directory.Delete(root, true);
        }

        [Fact]
        public void UnknownException_UsesStableUnexpectedCode()
        {
            var display = PdmNormalizeExportErrorFormatter.Format(new IOException("private details"));
            Assert.Contains("UNEXPECTED_NORMALIZE_EXPORT_FAILURE", display);
            Assert.DoesNotContain("private details", display);
        }

        [Fact]
        public void StructuredException_UsesCodeAndUserMessageOnly()
        {
            var display = PdmNormalizeExportErrorFormatter.Format(new PdmNormalizeExportException("PACKAGE_COMMIT_FAILED", "Không thể công bố package.", "private path"));
            Assert.Contains("PACKAGE_COMMIT_FAILED", display);
            Assert.Contains("Không thể công bố package.", display);
            Assert.DoesNotContain("private path", display);
        }

        [Fact]
        public void EscapingRelativeExternalLink_IsRejectedOutsidePackage()
        {
            var package = Path.Combine(Path.GetTempPath(), "pdm-ref-" + Guid.NewGuid().ToString("N"));
            var cad = Path.Combine(package, "cad"); Directory.CreateDirectory(cad);
            var result = PdmExternalReferencePolicy.Evaluate("..\\outside.ics", cad, cad, null, null, "outside.ics");
            Assert.Contains("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE", result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void MissingAndCanonicalMismatchExternalTarget_AreRejected()
        {
            var package = Path.Combine(Path.GetTempPath(), "pdm-ref-" + Guid.NewGuid().ToString("N"));
            var cad = Path.Combine(package, "cad"); Directory.CreateDirectory(cad);
            var result = PdmExternalReferencePolicy.Evaluate("missing.ics", cad, cad, null, null, "expected.ics");
            Assert.Contains("EXTERNAL_REFERENCE_MISSING", result.Issues);
            Assert.Contains("CANONICAL_REFERENCE_MISMATCH", result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void ExternalTargetPointingToSource_IsRejected()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-ref-" + Guid.NewGuid().ToString("N"));
            var cad = Path.Combine(root, "package", "cad"); var source = Path.Combine(root, "source");
            Directory.CreateDirectory(cad); Directory.CreateDirectory(source);
            var target = Path.Combine(source, "source.ics"); File.WriteAllText(target, "source");
            var result = PdmExternalReferencePolicy.Evaluate(target, cad, cad, source, null, "source.ics");
            Assert.Contains("EXTERNAL_REFERENCE_POINTS_TO_SOURCE", result.Issues);
            Directory.Delete(root, true);
        }

        [Fact]
        public void RoundTripComparer_DetectsRootAndScenePropertyMismatch()
        {
            var expected = CreatePlan("A", "ROOT-SCENE");
            var actual = CreatePlan("B", "CHANGED-SCENE");
            var issues = PdmRoundTripPlanComparer.Compare(expected, actual);
            Assert.Contains("ROOT_REVISION_MISMATCH", issues);
            Assert.Contains("SCENE_NAME_MISMATCH", issues);
        }

        [Fact]
        public void RoundTripComparer_DetectsProjectAndParentEdgeMismatch()
        {
            var expected = CreatePlan("A", "ROOT"); var actual = CreatePlan("A", "ROOT");
            actual.Parts[0].ProjectCode = "PDM-OTHER"; actual.Parts[0].ParentNodeId = "other-parent";
            var issues = PdmRoundTripPlanComparer.Compare(expected, actual);
            Assert.Contains("PROJECT_CODE_MISMATCH", issues);
            Assert.Contains("PARENT_EDGE_MISMATCH", issues);
        }

        [Fact]
        public void ReferenceTraversalGuard_DetectsActiveCycle()
        {
            var guard = new PdmReferenceTraversalGuard<object>(new PdmNormalizationLimits { MaxDepth = 4, MaxNodeCount = 10 });
            var node = new object(); guard.Enter(node, 0);
            Assert.Throws<PdmNormalizeExportException>(() => guard.Enter(node, 1));
            guard.Exit(node);
        }

        [Fact]
        public void ReferenceTraversalGuard_EnforcesNodeLimit_ButAllowsRepeatedAfterExit()
        {
            var guard = new PdmReferenceTraversalGuard<object>(new PdmNormalizationLimits { MaxDepth = 4, MaxNodeCount = 2 });
            var repeated = new object(); guard.Enter(repeated, 0); guard.Exit(repeated); guard.Enter(repeated, 0); guard.Exit(repeated);
            Assert.Throws<PdmNormalizeExportException>(() => guard.Enter(new object(), 0));
        }

        [Fact]
        public void ReferenceTraversalGuard_EnforcesDepthLimit()
        {
            var guard = new PdmReferenceTraversalGuard<object>(new PdmNormalizationLimits { MaxDepth = 1, MaxNodeCount = 10 });
            Assert.Throws<PdmNormalizeExportException>(() => guard.Enter(new object(), 2));
        }

        [Fact]
        public void ManifestFindNumbers_FollowSiblingOrderAndRestartPerParent()
        {
            var source = new PdmSourceNode { Kind = PdmNodeKind.SceneRoot, Name = "ROOT", Children = new[] {
                new PdmSourceNode { Kind = PdmNodeKind.Assembly, Name = "A01_GROUP", Children = new[] {
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "P01_ONE" },
                    new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "P02_TWO" } } },
                new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "P03_THREE" },
                new PdmSourceNode { Kind = PdmNodeKind.Part, Name = "P04_FOUR" } } };
            var plan = new PdmNormalizationPlanner().CreatePlan("PDM-TEST", "A", source);
            var manifest = PdmManifestV2Factory.Create(plan);
            Assert.Equal(new[] { 10, 20, 30 }, manifest.Occurrences.Where(o => o.ParentOccurrenceId == "occ-0").OrderBy(o => o.OccurrencePath).Select(o => o.FindNumber));
            Assert.Equal(new[] { 10, 20 }, manifest.Occurrences.Where(o => o.ParentOccurrenceId == "occ-0-0").OrderBy(o => o.OccurrencePath).Select(o => o.FindNumber));
        }

        [Fact]
        public void Cleanup_ClosesExportedBeforeStagedAndDeletesOnlyAfterClose()
        {
            var events = new System.Collections.Generic.List<string>();
            var cleanup = new PdmTransactionCleanup<string>("staged", "exported", null, null, "staging", null, null, true);
            var result = cleanup.Execute(
                document => events.Add("close:" + document),
                directory => events.Add("delete:" + directory));
            Assert.Equal(new[] { "close:exported", "close:staged", "delete:staging" }, events);
            Assert.True(result.IsSuccessful);
        }

        [Fact]
        public void Cleanup_DoesNotDeleteDirectoryWhileTrackedDocumentCloseFails()
        {
            var deleted = false;
            var cleanup = new PdmTransactionCleanup<string>("staged", null, null, null, "staging", null, null, true);
            var result = cleanup.Execute(
                document => { throw new InvalidOperationException("locked"); },
                directory => deleted = true);
            Assert.False(deleted);
            Assert.False(result.IsSuccessful);
            Assert.Contains(result.Issues, issue => issue.StartsWith("DOCUMENT_CLOSE_FAILED", StringComparison.Ordinal));
        }

        [Fact]
        public void Cleanup_FailureRemovesPendingAndFailedFinalAfterDocumentsClose()
        {
            var events = new System.Collections.Generic.List<string>();
            var cleanup = new PdmTransactionCleanup<string>(null, null, "pending-doc", "final-doc", "staging", "pending", "final", true);
            var result = cleanup.Execute(
                document => events.Add("close:" + document),
                directory => events.Add("delete:" + directory));
            Assert.Equal(new[] { "close:final-doc", "close:pending-doc", "delete:staging", "delete:pending", "delete:final" }, events);
            Assert.True(result.PendingDirectoryRemoved);
            Assert.True(result.FailedFinalDirectoryRemoved);
        }

        [Fact]
        public void Cleanup_IsIdempotentAndFailurePreventsSuccess()
        {
            var calls = 0;
            var cleanup = new PdmTransactionCleanup<string>(null, null, null, null, "staging", null, null, false);
            var first = cleanup.Execute(document => { }, directory => { calls++; throw new IOException("locked"); });
            var second = cleanup.Execute(document => { }, directory => calls++);
            Assert.Same(first, second);
            Assert.Equal(1, calls);
            Assert.False(first.IsSuccessful);
        }

        [Fact]
        public void Cleanup_SuccessLeavesFinalDocumentOpenAndNeverReceivesOriginalSource()
        {
            var closed = new System.Collections.Generic.List<string>();
            var cleanup = new PdmTransactionCleanup<string>("staged", "exported", null, "final", "staging", null, "final-dir", false);
            var result = cleanup.Execute(document => closed.Add(document), directory => { });
            Assert.Equal(new[] { "exported", "staged" }, closed);
            Assert.DoesNotContain("original", closed);
            Assert.Equal("final", cleanup.FinalPackageDocument);
            Assert.True(result.IsSuccessful);
        }

        [Fact]
        public void ManifestV2Serialization_OmitsEmptyLegacyProjection()
        {
            var json = new PdmPackageManifestWriter().Serialize(PdmManifestV2Factory.Create(CreatePlan("A", "ROOT")));
            Assert.DoesNotContain("legacyItemsProjection", json);
        }

        [Fact]
        public void PackageValidator_MalformedDefinitionPathReturnsStableIssue()
        {
            var package = CreateValidPackage(out var manifest);
            manifest.Definitions.Single().FileName = "cad/invalid\0.ics";
            var result = new PdmPackageValidator().Validate(package, manifest);
            Assert.Contains(PdmPackageValidationIssue.InvalidManifestPath, result.Issues);
            Directory.Delete(package, true);
        }

        [Fact]
        public void FailedFinalPackage_RollbackRemovesFinalDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "pdm-publish-" + Guid.NewGuid().ToString("N"));
            var staging = Path.Combine(root, "staging"); var pending = Path.Combine(root, "pending"); var final = Path.Combine(root, "final");
            Directory.CreateDirectory(staging);
            var transaction = new PdmPackagePublicationTransaction(staging, pending, final);
            transaction.MoveToPending(); transaction.CommitPending(); transaction.RollbackFinal();
            Assert.False(Directory.Exists(final));
            Directory.Delete(root, true);
        }

        private static string CreateValidPackage(out PdmPackageManifest manifest)
        {
            var package = Path.Combine(Path.GetTempPath(), "pdm-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(package, "cad"));
            File.WriteAllText(Path.Combine(package, "cad", "root.ics"), "root");
            var nodeId = Guid.NewGuid().ToString("D");
            manifest = new PdmPackageManifest
            {
                SchemaVersion = 2, ProjectCode = "PDM-TEST", Revision = "A", RootOccurrenceId = "occ-0", RootFile = "cad/root.ics",
                Definitions = new[] { new PdmManifestDefinition { DefinitionId = "def-0", NodeId = nodeId, ItemCode = "ROOT", ItemType = "ASM", DisplayName = "ROOT", Revision = "A", FileName = "cad/root.ics" } },
                Occurrences = new[] { new PdmManifestOccurrence { OccurrenceId = "occ-0", OccurrencePath = "0", DefinitionId = "def-0", FindNumber = 10 } },
                BomV2 = new PdmManifestBomV2[0]
            };
            return package;
        }

        private static PdmNormalizationPlan CreatePlan(string revision, string sceneName)
        {
            var plan = new PdmNormalizationPlan { ProjectCode = "PDM-TEST", Revision = revision };
            plan.Root = new PdmPlanItem { OccurrencePath = "0", NodeId = "root", ItemCode = "ROOT", ItemType = "ASM", DisplayName = "ROOT", SceneName = sceneName, ProjectCode = "PDM-TEST", Revision = revision, SourceKind = PdmNodeKind.SceneRoot };
            plan.Parts.Add(new PdmPlanItem { OccurrencePath = "0/0", ParentNodeId = "root", NodeId = "child", ItemCode = "A01", ItemType = "PRT", DisplayName = "CHILD", SceneName = sceneName, ProjectCode = "PDM-TEST", Revision = revision, SourceKind = PdmNodeKind.Part, Depth = 1 });
            return plan;
        }
    }
}
