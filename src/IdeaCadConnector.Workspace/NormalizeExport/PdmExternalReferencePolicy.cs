using System;
using System.IO;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmExternalReferenceEvaluation
    {
        public string ResolvedTargetPath { get; set; }
        public System.Collections.Generic.IList<string> Issues { get; } = new System.Collections.Generic.List<string>();
    }

    public static class PdmExternalReferencePolicy
    {
        public static string ResolveLinkTarget(string reportedLink, string documentDirectory)
        {
            if (string.IsNullOrWhiteSpace(reportedLink))
                throw new PdmNormalizeExportException("EXTERNAL_REFERENCE_PATH_INVALID", "Đường dẫn liên kết ngoài không hợp lệ.");
            try
            {
                if (Path.IsPathRooted(reportedLink)) return Path.GetFullPath(reportedLink);
                if (string.IsNullOrWhiteSpace(documentDirectory))
                    throw new PdmNormalizeExportException("EXTERNAL_REFERENCE_PATH_INVALID", "Không xác định được thư mục tài liệu chứa liên kết.");
                return Path.GetFullPath(Path.Combine(documentDirectory, reportedLink));
            }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex)
            {
                throw new PdmNormalizeExportException("EXTERNAL_REFERENCE_PATH_INVALID", "Đường dẫn liên kết ngoài không hợp lệ.", ex.Message, ex);
            }
        }

        public static bool IsWithinDirectory(string path, string directory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory)) return false;
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var boundary = root + Path.DirectorySeparatorChar;
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
        }

        public static PdmExternalReferenceEvaluation Evaluate(string reportedLink, string documentDirectory,
            string packageCadDirectory, string sourceDirectory, string stagingDirectory, string expectedFileName)
        {
            var result = new PdmExternalReferenceEvaluation();
            try { result.ResolvedTargetPath = ResolveLinkTarget(reportedLink, documentDirectory); }
            catch (PdmNormalizeExportException) { result.Issues.Add("EXTERNAL_REFERENCE_PATH_INVALID"); return result; }
            if (!File.Exists(result.ResolvedTargetPath)) result.Issues.Add("EXTERNAL_REFERENCE_MISSING");
            if (!IsWithinDirectory(result.ResolvedTargetPath, packageCadDirectory)) result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE");
            if (IsWithinDirectory(result.ResolvedTargetPath, sourceDirectory) || IsWithinDirectory(result.ResolvedTargetPath, stagingDirectory))
                result.Issues.Add("EXTERNAL_REFERENCE_POINTS_TO_SOURCE");
            if (!string.Equals(Path.GetExtension(result.ResolvedTargetPath), ".ics", StringComparison.OrdinalIgnoreCase))
                result.Issues.Add("EXTERNAL_REFERENCE_OUTSIDE_PACKAGE");
            if (!string.IsNullOrWhiteSpace(expectedFileName) &&
                !string.Equals(Path.GetFileName(result.ResolvedTargetPath), expectedFileName, StringComparison.OrdinalIgnoreCase))
                result.Issues.Add("CANONICAL_REFERENCE_MISMATCH");
            return result;
        }
    }

    public static class PdmRoundTripPlanComparer
    {
        public static System.Collections.Generic.IList<string> Compare(PdmNormalizationPlan expected, PdmNormalizationPlan actual)
        {
            var issues = new System.Collections.Generic.List<string>();
            if (expected == null || actual == null || expected.Root == null || actual.Root == null)
            { issues.Add("ROUND_TRIP_VALIDATION_FAILED"); return issues; }
            CompareValue(expected.Root.NodeId, actual.Root.NodeId, "ROOT_NODE_ID_MISMATCH", issues);
            CompareValue(expected.Root.ItemCode, actual.Root.ItemCode, "ROOT_ITEM_CODE_MISMATCH", issues);
            CompareValue(expected.Root.ProjectCode, actual.Root.ProjectCode, "ROOT_PROJECT_CODE_MISMATCH", issues);
            CompareValue(expected.Root.Revision, actual.Root.Revision, "ROOT_REVISION_MISMATCH", issues);
            var expectedItems = expected.Items.ToDictionary(i => i.OccurrencePath ?? string.Empty, StringComparer.Ordinal);
            var actualItems = actual.Items.ToDictionary(i => i.OccurrencePath ?? string.Empty, StringComparer.Ordinal);
            if (!expectedItems.Keys.OrderBy(x => x).SequenceEqual(actualItems.Keys.OrderBy(x => x))) issues.Add("OCCURRENCE_PATH_MISMATCH");
            foreach (var path in expectedItems.Keys.Intersect(actualItems.Keys))
            {
                var e = expectedItems[path]; var a = actualItems[path];
                if (e.SourceKind != a.SourceKind) issues.Add("NODE_KIND_MISMATCH");
                CompareValue(e.ParentNodeId, a.ParentNodeId, "PARENT_EDGE_MISMATCH", issues);
                CompareValue(e.NodeId, a.NodeId, "NODE_ID_MISMATCH", issues);
                CompareValue(e.ItemCode, a.ItemCode, "ITEM_CODE_MISMATCH", issues);
                CompareValue(e.ItemType, a.ItemType, "ITEM_TYPE_MISMATCH", issues);
                CompareValue(e.DisplayName, a.DisplayName, "DISPLAY_NAME_MISMATCH", issues);
                CompareValue(e.SceneName, a.SceneName, "SCENE_NAME_MISMATCH", issues);
                CompareValue(e.ProjectCode, a.ProjectCode, "PROJECT_CODE_MISMATCH", issues);
                CompareValue(e.Revision, a.Revision, "REVISION_MISMATCH", issues);
            }
            if (expected.Assemblies.Count != actual.Assemblies.Count) issues.Add("ASSEMBLY_COUNT_MISMATCH");
            if (expected.Parts.Count != actual.Parts.Count) issues.Add("PART_COUNT_MISMATCH");
            if (expected.Items.Select(i => i.Depth).DefaultIfEmpty(0).Max() != actual.Items.Select(i => i.Depth).DefaultIfEmpty(0).Max()) issues.Add("MAX_DEPTH_MISMATCH");
            return issues;
        }

        private static void CompareValue(string expected, string actual, string issue, System.Collections.Generic.ICollection<string> issues)
        {
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) && !issues.Contains(issue)) issues.Add(issue);
        }
    }
}
