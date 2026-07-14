using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdeaCadConnector.Workspace;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackageImportResult
    {
        public string ManifestPath { get; set; }
        public string PackageDirectory { get; set; }
        public PdmPackageManifest Manifest { get; set; }
        public PdmPackageValidationResult Validation { get; set; }
        public PdmFolderAnalysis FolderAnalysis { get; set; }
        public PdmBusinessStructureAnalysis BusinessStructure { get; set; }
    }

    public sealed class PdmPackageImportReader
    {
        public const string ManifestFileName = "pdm-bom-manifest.json";

        public string FindManifest(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath)) return null;
            if (File.Exists(selectedPath) &&
                string.Equals(Path.GetFileName(selectedPath), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(selectedPath);
            if (!Directory.Exists(selectedPath)) return null;

            var direct = Path.Combine(selectedPath, ManifestFileName);
            if (File.Exists(direct)) return Path.GetFullPath(direct);

            return Directory.GetFiles(selectedPath, ManifestFileName, SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }

        public PdmPackageImportResult Read(string selectedPath)
        {
            var manifestPath = FindManifest(selectedPath);
            if (manifestPath == null) throw new FileNotFoundException("PDM_MANIFEST_NOT_FOUND", ManifestFileName);
            var packageDirectory = Path.GetDirectoryName(manifestPath);
            PdmPackageManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<PdmPackageManifest>(File.ReadAllText(manifestPath));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("PDM_MANIFEST_INVALID_JSON", ex);
            }
            if (manifest == null) throw new InvalidDataException("PDM_MANIFEST_EMPTY");

            var validation = new PdmPackageValidator().Validate(packageDirectory, manifest);
            var folder = MapFolder(packageDirectory, manifest);
            foreach (var issue in validation.Issues)
            {
                folder.Issues.Add(new PdmNamingIssue
                {
                    FileName = ManifestFileName,
                    Message = "Manifest validation: " + issue,
                    BlocksPush = true
                });
            }

            return new PdmPackageImportResult
            {
                ManifestPath = manifestPath,
                PackageDirectory = packageDirectory,
                Manifest = manifest,
                Validation = validation,
                FolderAnalysis = folder,
                BusinessStructure = MapBusiness(packageDirectory, manifest)
            };
        }

        private static PdmFolderAnalysis MapFolder(string packageDirectory, PdmPackageManifest manifest)
        {
            var result = new PdmFolderAnalysis
            {
                FolderPath = packageDirectory,
                ProjectCode = manifest.ProjectCode,
                Version = manifest.Revision
            };
            foreach (var definition in manifest.Definitions ?? Enumerable.Empty<PdmManifestDefinition>())
            {
                var relative = definition.FileName ?? string.Empty;
                var parsed = new PdmParsedFile
                {
                    FullPath = ResolveSafePath(packageDirectory, relative),
                    RelativePath = relative,
                    FileName = Path.GetFileName(relative),
                    ProjectCode = manifest.ProjectCode,
                    Version = definition.Revision ?? manifest.Revision,
                    Revision = definition.Revision ?? manifest.Revision,
                    NodeType = string.Equals(definition.ItemType, "ASM", StringComparison.OrdinalIgnoreCase) ? "Assembly" : "Component",
                    LogicalPartCode = definition.ItemCode,
                    DisplayName = definition.DisplayName,
                    Status = "PDM Manifest V2"
                };
                result.TrackedFiles.Add(parsed);
                if (string.Equals(definition.ItemType, "ASM", StringComparison.OrdinalIgnoreCase)) result.AssemblyFiles.Add(parsed);
                else result.DetailFiles.Add(parsed);
                if (string.Equals(definition.NodeId, manifest.RootNodeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(definition.ItemCode, manifest.RootItemCode, StringComparison.OrdinalIgnoreCase))
                    result.PrimaryAssembly = parsed;
            }
            return result;
        }

        private static string ResolveSafePath(string packageDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
            try
            {
                var full = Path.GetFullPath(Path.Combine(packageDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                return PdmExternalReferencePolicy.IsWithinDirectory(full, packageDirectory) ? full : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        private static PdmBusinessStructureAnalysis MapBusiness(string packageDirectory, PdmPackageManifest manifest)
        {
            var result = new PdmBusinessStructureAnalysis
            {
                FolderPath = packageDirectory,
                ProjectCode = manifest.ProjectCode,
                RootDrawingFileName = Path.GetFileName(manifest.RootFile)
            };
            var definitions = (manifest.Definitions ?? Enumerable.Empty<PdmManifestDefinition>())
                .Where(definition => !string.IsNullOrWhiteSpace(definition.DefinitionId))
                .GroupBy(definition => definition.DefinitionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var occurrences = (manifest.Occurrences ?? Enumerable.Empty<PdmManifestOccurrence>()).ToList();
            var root = occurrences.FirstOrDefault(occurrence =>
                string.Equals(occurrence.OccurrenceId, manifest.RootOccurrenceId, StringComparison.OrdinalIgnoreCase));
            if (root == null) return result;

            foreach (var child in ChildrenOf(root.OccurrenceId, occurrences))
                result.RootNodes.Add(CreateBusinessNode(child, definitions, occurrences, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            return result;
        }

        private static PdmBusinessNode CreateBusinessNode(
            PdmManifestOccurrence occurrence,
            IDictionary<string, PdmManifestDefinition> definitions,
            IList<PdmManifestOccurrence> occurrences,
            ISet<string> active)
        {
            if (!active.Add(occurrence.OccurrenceId ?? string.Empty))
                throw new InvalidDataException("PDM_MANIFEST_OCCURRENCE_CYCLE");
            definitions.TryGetValue(occurrence.DefinitionId ?? string.Empty, out var definition);
            var node = new PdmBusinessNode
            {
                Code = definition?.ItemCode ?? occurrence.DefinitionId,
                Name = definition?.DisplayName ?? occurrence.DefinitionId,
                DisplayName = definition?.DisplayName ?? occurrence.DefinitionId,
                NodeType = string.Equals(definition?.ItemType, "ASM", StringComparison.OrdinalIgnoreCase) ? "Assembly" : "Component",
                SourceFileName = Path.GetFileName(definition?.FileName ?? string.Empty)
            };
            foreach (var child in ChildrenOf(occurrence.OccurrenceId, occurrences))
                node.Children.Add(CreateBusinessNode(child, definitions, occurrences, active));
            active.Remove(occurrence.OccurrenceId ?? string.Empty);
            return node;
        }

        private static IEnumerable<PdmManifestOccurrence> ChildrenOf(string parentId, IEnumerable<PdmManifestOccurrence> occurrences)
        {
            return occurrences
                .Where(occurrence => string.Equals(occurrence.ParentOccurrenceId, parentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(occurrence => occurrence.FindNumber)
                .ThenBy(occurrence => occurrence.OccurrencePath, StringComparer.Ordinal);
        }
    }
}
