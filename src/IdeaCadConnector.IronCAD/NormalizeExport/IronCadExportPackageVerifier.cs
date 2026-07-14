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
        public IronCadExportPackageVerifier(IronCadSceneNormalizationReader reader) { _reader = reader ?? throw new ArgumentNullException(nameof(reader)); }

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
                var expected = plan.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                var observed = actual.Items.ToDictionary(i => i.OccurrencePath, StringComparer.Ordinal);
                if (!expected.Keys.OrderBy(x => x).SequenceEqual(observed.Keys.OrderBy(x => x))) result.Issues.Add("STAGED_TREE_MISMATCH");
                foreach (var path in expected.Keys.Intersect(observed.Keys))
                {
                    var e = expected[path]; var a = observed[path];
                    if (!string.Equals(e.NodeId, a.NodeId, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.ItemCode, a.ItemCode, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.DisplayName, a.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(e.ItemType, a.ItemType, StringComparison.OrdinalIgnoreCase))
                        result.Issues.Add("ROUND_TRIP_VALIDATION_FAILED");
                }
                if (!string.IsNullOrWhiteSpace(packageRoot)) VerifyExternalReferences(exportedScene, packageRoot, sourceRoot, stagingRoot, plan, result);
            }
            catch (Exception ex) { result.Issues.Add("EXPORTED_ROOT_OPEN_FAILED"); System.Diagnostics.Trace.WriteLine(ex); }
            result.IsValid = result.Issues.Count == 0;
            if (!result.IsValid) throw new InvalidOperationException(result.Issues.First());
            return result;
        }

        private static void VerifyExternalReferences(IZSceneDoc scene, string packageRoot, string sourceRoot, string stagingRoot, PdmNormalizationPlan plan, IronCadRoundTripValidationResult result)
        {
            var cadRoot = System.IO.Path.Combine(System.IO.Path.GetFullPath(packageRoot), "cad");
            var root = scene.GetTopElement();
            VerifyElement(root, "0", cadRoot, sourceRoot, stagingRoot, plan, result);
        }

        private static void VerifyElement(IZElement element, string occurrencePath, string cadRoot, string sourceRoot, string stagingRoot, PdmNormalizationPlan plan, IronCadRoundTripValidationResult result)
        {
            string link = null; bool linked = false;
            var sceneElement = element as IZSceneElement;
            if (sceneElement != null) link = sceneElement.ModelLinkPath;
            var part = element as IZPart; var assembly = element as IZAssembly;
            if (part != null) { bool b; var p = part.GetExternallyLinkedInfo(out b); linked |= b; if (!string.IsNullOrWhiteSpace(p)) link = p; }
            if (assembly != null) { bool b; var p = assembly.GetExternallyLinkedInfo(out b); linked |= b; if (!string.IsNullOrWhiteSpace(p)) link = p; }
            if (linked || !string.IsNullOrWhiteSpace(link))
            {
                var reference = new IronCadExternalReferenceRecord { OccurrencePath = occurrencePath, ReportedLinkPath = link };
                result.ExternalReferences.Add(reference);
                if (string.IsNullOrWhiteSpace(link)) { result.Issues.Add("EXTERNAL_REFERENCE_MISSING"); }
                else
                {
                    var target = System.IO.Path.GetFullPath(link);
                    reference.ResolvedTargetPath = target;
                    reference.Exists = System.IO.File.Exists(target);
                    reference.InsidePackage = IsWithin(target, cadRoot);
                    reference.PointsToSource = IsWithin(target, sourceRoot) || (!string.IsNullOrWhiteSpace(stagingRoot) && IsWithin(target, stagingRoot));
                    reference.CanonicalFileNameMatch = reference.InsidePackage && string.Equals(System.IO.Path.GetExtension(target), ".ics", StringComparison.OrdinalIgnoreCase);
                    var expected = plan.Items.FirstOrDefault(i => string.Equals(i.OccurrencePath, occurrencePath, StringComparison.Ordinal));
                    if (expected != null) reference.CanonicalFileNameMatch = string.Equals(System.IO.Path.GetFileName(target), expected.CanonicalFileName, StringComparison.OrdinalIgnoreCase);
                    if (!reference.Exists) result.Issues.Add("EXTERNAL_REFERENCE_MISSING");
                    if (!reference.InsidePackage) result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE");
                    if (reference.PointsToSource) result.Issues.Add("EXTERNAL_REFERENCE_POINTS_TO_SOURCE");
                    if (!reference.CanonicalFileNameMatch) result.Issues.Add("CANONICAL_REFERENCE_MISMATCH");
                    if (!string.Equals(System.IO.Path.GetExtension(target), ".ics", StringComparison.OrdinalIgnoreCase)) result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE");
                }
            }
            var children = element.GetChildrenZArray(); int count = 0; if (children == null) return; children.Count(out count);
            for (var i = 0; i < count; i++) { object value; children.Get(i, out value); var child = value as IZElement; if (child != null) VerifyElement(child, occurrencePath + "/" + i, cadRoot, sourceRoot, stagingRoot, plan, result); }
        }

        private static bool IsWithin(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return false;
            var boundary = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
        }
    }
}
