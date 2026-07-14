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
            if (manifest.SchemaVersion != 2) result.Issues.Add(PdmPackageValidationIssue.InvalidSchemaVersion);
            var definitions = (manifest.Definitions ?? Enumerable.Empty<PdmManifestDefinition>()).ToList();
            var occurrences = (manifest.Occurrences ?? Enumerable.Empty<PdmManifestOccurrence>()).ToList();
            var manifestIds = definitions.Select(d => d.DefinitionId).Concat(occurrences.Select(o => o.OccurrenceId)).ToList();
            if (manifestIds.Any(string.IsNullOrWhiteSpace) || manifestIds.Count != manifestIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                result.Issues.Add(PdmPackageValidationIssue.DuplicateManifestId);
            if (occurrences.GroupBy(o => o.OccurrencePath ?? string.Empty, StringComparer.Ordinal).Any(g => g.Count() > 1))
                result.Issues.Add(PdmPackageValidationIssue.DuplicateOccurrencePath);
            var definitionIds = new HashSet<string>(definitions.Select(d => d.DefinitionId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            if (occurrences.Any(o => string.IsNullOrWhiteSpace(o.DefinitionId) || !definitionIds.Contains(o.DefinitionId)))
                result.Issues.Add(PdmPackageValidationIssue.MissingDefinition);
            var occurrenceIds = new HashSet<string>(occurrences.Select(o => o.OccurrenceId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var roots = occurrences.Where(o => string.IsNullOrWhiteSpace(o.ParentOccurrenceId)).ToList();
            if (string.IsNullOrWhiteSpace(manifest.RootOccurrenceId) || roots.Count != 1 || !occurrenceIds.Contains(manifest.RootOccurrenceId) || roots[0].OccurrenceId != manifest.RootOccurrenceId)
                result.Issues.Add(PdmPackageValidationIssue.RootOccurrenceInvalid);
            if (occurrences.Any(o => !string.IsNullOrWhiteSpace(o.ParentOccurrenceId) && !occurrenceIds.Contains(o.ParentOccurrenceId)))
                result.Issues.Add(PdmPackageValidationIssue.UnknownOccurrence);
            if (occurrences.Any(o => !string.IsNullOrWhiteSpace(o.ParentOccurrenceId) && !ParentPathIsCompatible(o, occurrences)))
                result.Issues.Add(PdmPackageValidationIssue.ParentPathMismatch);
            if (HasOccurrenceCycle(occurrences)) result.Issues.Add(PdmPackageValidationIssue.OccurrenceCycle);
            var referencedDefinitions = new HashSet<string>(occurrences.Select(o => o.DefinitionId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            if (definitions.Any(d => !referencedDefinitions.Contains(d.DefinitionId ?? string.Empty))) result.Issues.Add(PdmPackageValidationIssue.OrphanDefinition);
            foreach (var definition in definitions)
                CheckFile(packageDirectory, definition.FileName, result, PdmPackageValidationIssue.MissingDefinitionFile);
            if (manifest.BomV2 != null && manifest.BomV2.Any(e => e.Quantity <= 0 || !string.Equals(e.QuantityStatus, "IdentityUnavailable", StringComparison.OrdinalIgnoreCase)))
                result.Issues.Add(PdmPackageValidationIssue.InvalidQuantity);
            foreach (var path in definitions.Select(d => d.FileName).Concat(new[] { manifest.RootFile }))
                if (!IsSafeRelativePath(path)) result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath);
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

            var edges = manifest.BomV2 ?? Enumerable.Empty<PdmManifestBomV2>();
            if (edges.Any(e => !occurrenceIds.Contains(e.ParentOccurrenceId ?? string.Empty) || !definitionIds.Contains(e.ChildDefinitionId ?? string.Empty)))
                result.Issues.Add(PdmPackageValidationIssue.UnknownBomNode);
            return result;
        }

        private static bool ParentPathIsCompatible(PdmManifestOccurrence occurrence, IList<PdmManifestOccurrence> all)
        {
            var parent = all.FirstOrDefault(o => string.Equals(o.OccurrenceId, occurrence.ParentOccurrenceId, StringComparison.OrdinalIgnoreCase));
            if (parent == null) return false;
            var path = occurrence.OccurrencePath ?? string.Empty;
            return path.StartsWith((parent.OccurrencePath ?? string.Empty) + "/", StringComparison.Ordinal) &&
                path.Count(c => c == '/') == (parent.OccurrencePath ?? string.Empty).Count(c => c == '/') + 1;
        }

        private static bool HasOccurrenceCycle(IEnumerable<PdmManifestOccurrence> occurrences)
        {
            var map = occurrences.ToDictionary(o => o.OccurrenceId ?? string.Empty, o => o.ParentOccurrenceId, StringComparer.OrdinalIgnoreCase);
            foreach (var id in map.Keys)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var current = id;
                while (!string.IsNullOrWhiteSpace(current) && map.ContainsKey(current))
                {
                    if (!seen.Add(current)) return true;
                    current = map[current];
                }
            }
            return false;
        }

        private static bool IsSafeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return false;
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return !normalized.Split(Path.DirectorySeparatorChar).Any(p => p == ".." || p == ".");
        }

        private static void CheckFile(string directory, string relativePath,
            PdmPackageValidationResult result, PdmPackageValidationIssue issue)
        {
            if (!IsSafeRelativePath(relativePath) || !IsWithin(Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar)), directory) ||
                !File.Exists(Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                result.Issues.Add(issue);
        }

        private static bool IsWithin(string path, string root)
        {
            var full = Path.GetFullPath(path); var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase);
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
