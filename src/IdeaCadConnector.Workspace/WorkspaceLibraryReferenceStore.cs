using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceLibraryReferenceStore
    {
        public const int CurrentSchemaVersion = 1;

        private readonly WorkspaceService _workspaceService;

        public WorkspaceLibraryReferenceStore(WorkspaceService workspaceService)
        {
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        }

        public string GetFilePath(string projectFolder)
        {
            var dir = _workspaceService.GetManifestDirectory(projectFolder);
            return dir == null ? null : Path.Combine(dir, "library-references.json");
        }

        public IReadOnlyList<WorkspaceLibraryReference> Load(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return Array.Empty<WorkspaceLibraryReference>();

            var path = GetFilePath(projectFolder);
            if (path == null || !File.Exists(path))
                return Array.Empty<WorkspaceLibraryReference>();

            try
            {
                var json = File.ReadAllText(path);
                var token = JToken.Parse(json);

                if (token.Type == JTokenType.Array)
                {
                    var legacyEntries = token.ToObject<List<WorkspaceLibraryReference>>();
                    return legacyEntries ?? (IReadOnlyList<WorkspaceLibraryReference>)Array.Empty<WorkspaceLibraryReference>();
                }

                var document = token.ToObject<WorkspaceLibraryReferenceDocument>();
                if (document?.References == null)
                    return Array.Empty<WorkspaceLibraryReference>();

                return document.References;
            }
            catch
            {
                return Array.Empty<WorkspaceLibraryReference>();
            }
        }

        public void Save(string projectFolder, IReadOnlyList<WorkspaceLibraryReference> references)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return;

            var path = GetFilePath(projectFolder);
            if (path == null)
                return;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var document = new WorkspaceLibraryReferenceDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                References = references ?? Array.Empty<WorkspaceLibraryReference>()
            };

            var json = JsonConvert.SerializeObject(document, Formatting.Indented);
            _workspaceService.WriteAllTextAtomic(path, json);
        }

        public void Upsert(string projectFolder, WorkspaceLibraryReference reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            var entries = Load(projectFolder).ToList();
            var existingIndex = entries.FindIndex(entry => string.Equals(entry.ReferenceId, reference.ReferenceId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
                entries[existingIndex] = reference;
            else
                entries.Add(reference);

            Save(projectFolder, entries);
        }

        public bool Remove(string projectFolder, string referenceId)
        {
            if (string.IsNullOrWhiteSpace(projectFolder) || string.IsNullOrWhiteSpace(referenceId))
                return false;

            var entries = Load(projectFolder).ToList();
            var removed = entries.RemoveAll(entry => string.Equals(entry.ReferenceId, referenceId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
                return false;

            Save(projectFolder, entries);
            return true;
        }
    }

    internal sealed class WorkspaceLibraryReferenceDocument
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("references")]
        public IReadOnlyList<WorkspaceLibraryReference> References { get; set; }
    }
}
