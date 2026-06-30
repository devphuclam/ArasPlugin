using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Core.Validation;
using Newtonsoft.Json;

namespace IdeaCadConnector.Workspace
{
    public sealed class WorkspaceService
    {
        private readonly WorkspaceOptions _options;

        public WorkspaceService(WorkspaceOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
        }

        public string GetCadPartPath(string partNumber)
        {
            var fileName = CadFileNamingRules.GetLocalPlaceholderFileName(partNumber);
            var root = string.IsNullOrWhiteSpace(_options.RootPath)
                ? GetDefaultRootPath()
                : _options.RootPath;

            var company = string.IsNullOrWhiteSpace(_options.CompanyCode)
                ? _options.DefaultCompanyCode
                : _options.CompanyCode.Trim();

            return Path.Combine(root, company, partNumber, fileName);
        }

        public void EnsureDirectoryForFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public string GetManifestDirectory(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return null;
            var dir = Path.Combine(projectFolder, ".idea-pdm");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetManifestFilePath(string projectFolder)
        {
            var dir = GetManifestDirectory(projectFolder);
            return dir == null ? null : Path.Combine(dir, "workspace.json");
        }

        public WorkspaceManifest LoadManifest(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return null;
            var path = GetManifestFilePath(projectFolder);
            if (path == null || !File.Exists(path))
                return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<WorkspaceManifest>(json);
            }
            catch
            {
                return null;
            }
        }

        public void SaveManifest(WorkspaceManifest manifest)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.ProjectFolder))
                return;
            var dir = GetManifestDirectory(manifest.ProjectFolder);
            if (dir == null) return;
            var path = Path.Combine(dir, "workspace.json");
            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void ClearManifest(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return;
            var path = GetManifestFilePath(projectFolder);
            if (path != null && File.Exists(path))
                File.Delete(path);
        }

        public string GetCommitHistoryFilePath(string projectFolder)
        {
            var dir = GetManifestDirectory(projectFolder);
            return dir == null ? null : Path.Combine(dir, "commits.json");
        }

        // TODO(PERF-INTERFACE): Extract IWorkspaceCommitStore from
        // LoadCommitHistory / SaveCommitHistory.
        // TODO(PERF-ERROR-HANDLING): Add logging in catch blocks.
        // Currently returns empty defaults silently on any error.
        public WorkspaceCommitHistory LoadCommitHistory(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return new WorkspaceCommitHistory();
            var path = GetCommitHistoryFilePath(projectFolder);
            if (path == null || !File.Exists(path))
                return new WorkspaceCommitHistory();
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<WorkspaceCommitHistory>(json) ?? new WorkspaceCommitHistory();
            }
            catch
            {
                return new WorkspaceCommitHistory();
            }
        }

        public void SaveCommitHistory(string projectFolder, WorkspaceCommitHistory history)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return;
            var path = GetCommitHistoryFilePath(projectFolder);
            if (path == null) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(history, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public string GetBranchRegistryFilePath(string projectFolder)
        {
            var dir = GetManifestDirectory(projectFolder);
            return dir == null ? null : Path.Combine(dir, "branches.json");
        }

        // TODO(PERF-INTERFACE): Extract IWorkspaceBranchStore from
        // LoadBranchRegistry / SaveBranchRegistry / EnsureMainBranch.
        // TODO(PERF-ERROR-HANDLING): Add logging in catch blocks.
        // Currently returns empty defaults silently on any error.
        public WorkspaceBranchRegistry LoadBranchRegistry(string projectFolder)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return new WorkspaceBranchRegistry();
            var path = GetBranchRegistryFilePath(projectFolder);
            if (path == null || !File.Exists(path))
                return new WorkspaceBranchRegistry();
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<WorkspaceBranchRegistry>(json) ?? new WorkspaceBranchRegistry();
            }
            catch
            {
                return new WorkspaceBranchRegistry();
            }
        }

        public void SaveBranchRegistry(string projectFolder, WorkspaceBranchRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(projectFolder))
                return;
            var path = GetBranchRegistryFilePath(projectFolder);
            if (path == null) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(registry, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void EnsureMainBranch(string projectFolder)
        {
            var registry = LoadBranchRegistry(projectFolder);
            if (registry.Branches.Any(b => string.Equals(b.Name, "main", StringComparison.OrdinalIgnoreCase)))
                return;
            registry.Branches.Add(new WorkspaceBranch
            {
                Name = "main",
                CreatedAt = DateTime.UtcNow
            });
            SaveBranchRegistry(projectFolder, registry);
        }

        private static string GetDefaultRootPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Idea", "ArasCadWorkspace");
        }
    }
}
