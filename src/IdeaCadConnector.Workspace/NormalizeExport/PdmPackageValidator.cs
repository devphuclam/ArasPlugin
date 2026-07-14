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
            if (manifest.SchemaVersion != 2) result.Issues.Add(PdmPackageValidationIssue.InvalidSchemaVersion);
            var definitions = (manifest.Definitions ?? Enumerable.Empty<PdmManifestDefinition>()).ToList();
            var occurrences = (manifest.Occurrences ?? Enumerable.Empty<PdmManifestOccurrence>()).ToList();
            var bom = (manifest.BomV2 ?? Enumerable.Empty<PdmManifestBomV2>()).ToList();

            CheckDuplicates(definitions.Select(d => d.DefinitionId), PdmPackageValidationIssue.DuplicateManifestId, result);
            CheckDuplicates(definitions.Select(d => d.NodeId), PdmPackageValidationIssue.DuplicateManifestId, result);
            CheckDuplicates(definitions.Select(d => d.ItemCode), PdmPackageValidationIssue.DuplicateItemCode, result);
            CheckDuplicates(definitions.Select(d => d.FileName), PdmPackageValidationIssue.DuplicateFileName, result);
            CheckDuplicates(occurrences.Select(o => o.OccurrenceId), PdmPackageValidationIssue.DuplicateManifestId, result);
            CheckDuplicates(occurrences.Select(o => o.OccurrencePath), PdmPackageValidationIssue.DuplicateOccurrencePath, result);

            var definitionIds = new HashSet<string>(definitions.Select(d => d.DefinitionId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var occurrenceIds = new HashSet<string>(occurrences.Select(o => o.OccurrenceId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var roots = occurrences.Where(o => string.IsNullOrWhiteSpace(o.ParentOccurrenceId)).ToList();
            if (roots.Count != 1 || string.IsNullOrWhiteSpace(manifest.RootOccurrenceId) ||
                !string.Equals(roots[0].OccurrenceId, manifest.RootOccurrenceId, StringComparison.OrdinalIgnoreCase))
                result.Issues.Add(PdmPackageValidationIssue.RootOccurrenceInvalid);
            if (occurrences.Any(o => string.IsNullOrWhiteSpace(o.DefinitionId) || !definitionIds.Contains(o.DefinitionId)))
                result.Issues.Add(PdmPackageValidationIssue.MissingDefinition);
            if (occurrences.Any(o => !string.IsNullOrWhiteSpace(o.ParentOccurrenceId) && !occurrenceIds.Contains(o.ParentOccurrenceId)))
                result.Issues.Add(PdmPackageValidationIssue.UnknownOccurrence);
            if (occurrences.Any(o => !string.IsNullOrWhiteSpace(o.ParentOccurrenceId) && !ParentPathIsCompatible(o, occurrences)))
                result.Issues.Add(PdmPackageValidationIssue.ParentPathMismatch);
            if (HasOccurrenceCycle(occurrences)) result.Issues.Add(PdmPackageValidationIssue.OccurrenceCycle);
            var referencedDefinitions = new HashSet<string>(occurrences.Select(o => o.DefinitionId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            if (definitions.Any(d => !referencedDefinitions.Contains(d.DefinitionId ?? string.Empty)))
                result.Issues.Add(PdmPackageValidationIssue.OrphanDefinition);
            if (bom.Any(e => !occurrenceIds.Contains(e.ParentOccurrenceId ?? string.Empty) || !definitionIds.Contains(e.ChildDefinitionId ?? string.Empty)))
                result.Issues.Add(PdmPackageValidationIssue.UnknownBomNode);
            if (bom.Any(e => e.Quantity <= 0 || !string.Equals(e.QuantityStatus, "IdentityUnavailable", StringComparison.Ordinal)))
                result.Issues.Add(PdmPackageValidationIssue.InvalidQuantity);

            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddExpectedPath(packageDirectory, manifest.RootFile, PdmPackageValidationIssue.MissingRootFile, expected, result);
            var definitionTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
                AddExpectedPath(packageDirectory, definition.FileName, PdmPackageValidationIssue.MissingDefinitionFile, expected, result, definitionTargets);
            var cadDirectory = Path.Combine(packageDirectory, "cad");
            if (Directory.Exists(cadDirectory))
            {
                foreach (var actual in Directory.GetFiles(cadDirectory, "*.ics", SearchOption.AllDirectories).Select(Path.GetFullPath))
                    if (!expected.Contains(actual)) result.Issues.Add(PdmPackageValidationIssue.OrphanFile);
            }
            return result;
        }

        private static void CheckDuplicates(IEnumerable<string> values, PdmPackageValidationIssue issue, PdmPackageValidationResult result)
        {
            var list = values.ToList();
            if (list.Any(string.IsNullOrWhiteSpace) || list.GroupBy(v => v ?? string.Empty, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                result.Issues.Add(issue);
        }

        private static void AddExpectedPath(string packageRoot, string relativePath, PdmPackageValidationIssue missingIssue,
            ISet<string> expected, PdmPackageValidationResult result, ISet<string> duplicateTargets = null)
        {
            try
            {
                if (!IsSafeRelativePath(relativePath)) { result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath); return; }
                var full = Path.GetFullPath(Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!PdmExternalReferencePolicy.IsWithinDirectory(full, packageRoot)) { result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath); return; }
                expected.Add(full);
                if (duplicateTargets != null && !duplicateTargets.Add(full))
                    result.Issues.Add(PdmPackageValidationIssue.DuplicateFileName);
                if (!File.Exists(full)) result.Issues.Add(missingIssue);
            }
            catch (ArgumentException) { result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath); }
            catch (NotSupportedException) { result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath); }
            catch (PathTooLongException) { result.Issues.Add(PdmPackageValidationIssue.InvalidManifestPath); }
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
            return !path.Replace('/', Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Any(p => p == "." || p == "..");
        }

        private static bool ParentPathIsCompatible(PdmManifestOccurrence occurrence, IList<PdmManifestOccurrence> all)
        {
            var parent = all.FirstOrDefault(o => string.Equals(o.OccurrenceId, occurrence.ParentOccurrenceId, StringComparison.OrdinalIgnoreCase));
            if (parent == null) return false;
            var expectedPrefix = (parent.OccurrencePath ?? string.Empty) + "/";
            return (occurrence.OccurrencePath ?? string.Empty).StartsWith(expectedPrefix, StringComparison.Ordinal) &&
                occurrence.OccurrencePath.Count(c => c == '/') == (parent.OccurrencePath ?? string.Empty).Count(c => c == '/') + 1;
        }

        private static bool HasOccurrenceCycle(IEnumerable<PdmManifestOccurrence> occurrences)
        {
            var map = occurrences.ToDictionary(o => o.OccurrenceId ?? string.Empty, o => o.ParentOccurrenceId, StringComparer.OrdinalIgnoreCase);
            foreach (var id in map.Keys)
            {
                var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var current = id;
                while (!string.IsNullOrWhiteSpace(current) && map.ContainsKey(current))
                {
                    if (!active.Add(current)) return true;
                    current = map[current];
                }
            }
            return false;
        }
    }
}
