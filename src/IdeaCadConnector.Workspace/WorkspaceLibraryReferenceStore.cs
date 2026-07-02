using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceLibraryReferenceStore
    {
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
                var entries = JsonConvert.DeserializeObject<List<WorkspaceLibraryReference>>(json);
                return entries ?? (IReadOnlyList<WorkspaceLibraryReference>)Array.Empty<WorkspaceLibraryReference>();
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

            var json = JsonConvert.SerializeObject(references ?? Array.Empty<WorkspaceLibraryReference>(), Formatting.Indented);
            File.WriteAllText(path, json);
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
    }
}
