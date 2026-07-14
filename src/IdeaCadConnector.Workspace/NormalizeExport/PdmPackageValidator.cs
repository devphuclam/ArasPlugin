using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackageValidator
    {
        public PdmPackageValidationResult Validate(string packageDirectory, PdmPackageManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var result = new PdmPackageValidationResult();
            var items = (manifest.Items ?? Enumerable.Empty<PdmManifestItem>()).ToList();
            var known = new HashSet<string>(items.Select(i => i.NodeId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            known.Add(manifest.RootNodeId ?? string.Empty);

            CheckFile(packageDirectory, manifest.RootFile, result, PdmPackageValidationIssue.MissingRootFile);
            foreach (var item in items)
                CheckFile(packageDirectory, item.FileName, result, PdmPackageValidationIssue.MissingFile);

            if (items.Where(i => !string.IsNullOrWhiteSpace(i.ItemCode))
                .GroupBy(i => i.ItemCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                result.Issues.Add(PdmPackageValidationIssue.DuplicateItemCode);
            if (items.Where(i => !string.IsNullOrWhiteSpace(i.FileName))
                .GroupBy(i => i.FileName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                result.Issues.Add(PdmPackageValidationIssue.DuplicateFileName);

            var edges = manifest.Bom ?? Enumerable.Empty<PdmManifestBomEdge>();
            if (edges.Any(e => !known.Contains(e.ParentNodeId ?? string.Empty) || !known.Contains(e.ChildNodeId ?? string.Empty)))
                result.Issues.Add(PdmPackageValidationIssue.UnknownBomNode);
            if (HasCycle(edges)) result.Issues.Add(PdmPackageValidationIssue.BomCycle);
            return result;
        }

        private static void CheckFile(string directory, string relativePath,
            PdmPackageValidationResult result, PdmPackageValidationIssue issue)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || !File.Exists(Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                result.Issues.Add(issue);
        }

        private static bool HasCycle(IEnumerable<PdmManifestBomEdge> edges)
        {
            var graph = edges.GroupBy(e => e.ParentNodeId ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ChildNodeId ?? string.Empty).ToList(), StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in graph.Keys)
                if (Visit(node, graph, visiting, visited)) return true;
            return false;
        }

        private static bool Visit(string node, IDictionary<string, List<string>> graph,
            ISet<string> visiting, ISet<string> visited)
        {
            if (visiting.Contains(node)) return true;
            if (visited.Contains(node)) return false;
            visiting.Add(node);
            List<string> children;
            if (graph.TryGetValue(node, out children))
                foreach (var child in children)
                    if (Visit(child, graph, visiting, visited)) return true;
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }
    }
}
