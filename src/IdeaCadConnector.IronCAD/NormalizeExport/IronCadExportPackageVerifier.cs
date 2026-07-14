using System;
using System.Collections.Generic;
using System.Linq;
using interop.ICApiIronCAD;
using IdeaCadConnector.Workspace.NormalizeExport;

namespace IdeaCadConnector.IronCAD.NormalizeExport
{
    public sealed class IronCadRoundTripValidationResult
    {
        public bool IsValid { get; set; }
        public IList<string> Issues { get; } = new List<string>();
        public IList<IronCadExternalReferenceRecord> ExternalReferences { get; } = new List<IronCadExternalReferenceRecord>();
    }

    public sealed class IronCadExternalReferenceRecord
    {
        public string OccurrencePath { get; set; }
        public string ReportedLinkPath { get; set; }
        public string ResolvedTargetPath { get; set; }
        public bool Exists { get; set; }
        public bool InsidePackage { get; set; }
        public bool PointsToSource { get; set; }
        public bool CanonicalFileNameMatch { get; set; }
    }

    public sealed class IronCadExportPackageVerifier
    {
        private readonly IronCadSceneNormalizationReader _reader;
        private readonly PdmNormalizationLimits _limits;
        public IronCadExportPackageVerifier(IronCadSceneNormalizationReader reader, PdmNormalizationLimits limits = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _limits = limits ?? new PdmNormalizationLimits();
        }

        public IronCadRoundTripValidationResult Verify(IZSceneDoc exportedScene, PdmNormalizationPlan plan)
        {
            return Verify(exportedScene, plan, null, null, null);
        }

        public IronCadRoundTripValidationResult Verify(IZSceneDoc exportedScene, PdmNormalizationPlan plan,
            string packageRoot, string sourceRoot, string stagingRoot)
        {
            var result = new IronCadRoundTripValidationResult();
            try
            {
                var snapshot = _reader.Read(exportedScene);
                var actual = new PdmNormalizationPlanner().CreatePlan(plan.ProjectCode, plan.Revision, snapshot.Root);
                ApplyObservedProperties(actual.Root, snapshot.Root);
                foreach (var item in actual.Items) ApplyObservedProperties(item, item.SourceNode);
                foreach (var issue in PdmRoundTripPlanComparer.Compare(plan, actual)) result.Issues.Add(issue);
                if (!string.IsNullOrWhiteSpace(packageRoot)) VerifyExternalReferences(exportedScene, packageRoot, sourceRoot, stagingRoot, plan, result, _limits);
            }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex) { throw new PdmNormalizeExportException("ROUND_TRIP_VALIDATION_FAILED", "Không thể xác minh package sau khi mở lại.", ex.ToString(), ex); }
            result.IsValid = result.Issues.Count == 0;
            if (!result.IsValid) throw new PdmNormalizeExportException("ROUND_TRIP_VALIDATION_FAILED", "Package mở lại không khớp kế hoạch đã phê duyệt.", string.Join(",", result.Issues));
            return result;
        }

        private static void ApplyObservedProperties(PdmPlanItem item, PdmSourceNode source)
        {
            if (item == null || source == null) return;
            var properties = source.Properties ?? new PdmSourceProperties();
            item.NodeId = properties.NodeId;
            item.ItemCode = properties.ItemCode;
            item.ItemType = properties.ItemType;
            item.DisplayName = properties.DisplayName;
            item.ProjectCode = properties.ProjectCode;
            item.Revision = properties.Revision;
            item.SceneName = source.Name;
        }

        private static void VerifyExternalReferences(IZSceneDoc scene, string packageRoot, string sourceRoot, string stagingRoot,
            PdmNormalizationPlan plan, IronCadRoundTripValidationResult result, PdmNormalizationLimits limits)
        {
            var cadRoot = System.IO.Path.Combine(System.IO.Path.GetFullPath(packageRoot), "cad");
            var root = scene.GetTopElement();
            var guard = new PdmReferenceTraversalGuard<IZElement>(limits);
            VerifyElement(root, "0", 0, cadRoot, sourceRoot, stagingRoot, plan, result, guard);
        }

        private static void VerifyElement(IZElement element, string occurrencePath, int depth, string cadRoot, string sourceRoot,
            string stagingRoot, PdmNormalizationPlan plan, IronCadRoundTripValidationResult result,
            PdmReferenceTraversalGuard<IZElement> guard)
        {
            guard.Enter(element, depth);
            try
            {
                string link = null; bool linked = false;
                var sceneElement = element as IZSceneElement;
                if (sceneElement != null)
                {
                    try { link = sceneElement.ModelLinkPath; }
                    catch (Exception ex) when (IronCadDependencyDiscovery.IsIgnorableModelLinkPathFailure(ex))
                    {
                        link = null;
                    }
                }
                var part = element as IZPart; var assembly = element as IZAssembly;
                if (part != null) { bool b; var p = part.GetExternallyLinkedInfo(out b); linked |= b; if (!string.IsNullOrWhiteSpace(p)) link = p; }
                if (assembly != null) { bool b; var p = assembly.GetExternallyLinkedInfo(out b); linked |= b; if (!string.IsNullOrWhiteSpace(p)) link = p; }
                if (linked || !string.IsNullOrWhiteSpace(link))
                {
                    var expected = new[] { plan.Root }.Concat(plan.Items)
                        .FirstOrDefault(i => string.Equals(i.OccurrencePath, occurrencePath, StringComparison.Ordinal));
                    var evaluation = PdmExternalReferencePolicy.Evaluate(link, cadRoot, cadRoot, sourceRoot, stagingRoot,
                        expected == null ? null : expected.CanonicalFileName);
                    var reference = new IronCadExternalReferenceRecord
                    {
                        OccurrencePath = occurrencePath, ReportedLinkPath = link,
                        ResolvedTargetPath = evaluation.ResolvedTargetPath,
                        Exists = !evaluation.Issues.Contains("EXTERNAL_REFERENCE_MISSING"),
                        InsidePackage = !evaluation.Issues.Contains("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE"),
                        PointsToSource = evaluation.Issues.Contains("EXTERNAL_REFERENCE_POINTS_TO_SOURCE"),
                        CanonicalFileNameMatch = !evaluation.Issues.Contains("CANONICAL_REFERENCE_MISMATCH")
                    };
                    result.ExternalReferences.Add(reference);
                    foreach (var issue in evaluation.Issues) result.Issues.Add(issue);
                }
                IZArray children;
                try { children = element.GetChildrenZArray(); }
                catch (Exception ex) when (IronCadDependencyDiscovery.IsIgnorableModelLinkPathFailure(ex))
                {
                    return;
                }
                int count = 0; if (children == null) return; children.Count(out count);
                for (var i = 0; i < count; i++)
                {
                    object value; children.Get(i, out value); var child = value as IZElement;
                    if (child == null) throw new PdmNormalizeExportException("ROUND_TRIP_VALIDATION_FAILED", "Không thể đọc child occurrence trong package.");
                    VerifyElement(child, occurrencePath + "/" + i, depth + 1, cadRoot, sourceRoot, stagingRoot,
                        plan, result, guard);
                }
            }
            finally { guard.Exit(element); }
        }
    }
}
