using System;
using System.IO;

namespace IdeaCadConnector.Workspace.BomDiagnostic
{
    public static class BomDiagnosticOutputPathPolicy
    {
        public static string Validate(string outputFolder, string repositoryRoot = null,
            string studyDirectory = null, string applicationDataDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("An explicit external diagnostic output folder is required.", nameof(outputFolder));
            if (File.Exists(outputFolder))
                throw new InvalidOperationException("Diagnostic output path must be a directory, not a file: " + outputFolder);
            if (!Directory.Exists(outputFolder))
                throw new DirectoryNotFoundException("Diagnostic output folder does not exist: " + outputFolder);
            var fullOutput = Normalize(outputFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            repositoryRoot = repositoryRoot ?? FindRepositoryRoot();
            applicationDataDirectory = applicationDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var protectedRoots = new[]
            {
                repositoryRoot,
                studyDirectory,
                applicationDataDirectory,
                CombineIfPresent(repositoryRoot, ".git"),
                CombineIfPresent(repositoryRoot, "src"),
                CombineIfPresent(repositoryRoot, "tests"),
                CombineIfPresent(repositoryRoot, "docs"),
                CombineIfPresent(repositoryRoot, "tasks"),
                CombineIfPresent(repositoryRoot, "bin"),
                CombineIfPresent(repositoryRoot, "obj"),
                CombineIfPresent(repositoryRoot, ".vs"),
                CombineIfPresent(repositoryRoot, ".ai-work"),
                CombineIfPresent(repositoryRoot, "TestResults")
            };
            foreach (var root in protectedRoots)
            {
                if (!string.IsNullOrWhiteSpace(root) && IsSameOrDescendant(fullOutput, Normalize(root)))
                    throw new InvalidOperationException(
                        "Raw diagnostic output is restricted to an external folder; protected path rejected: " + fullOutput);
            }
            return fullOutput;
        }

        private static string CombineIfPresent(string root, string child)
        {
            return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, child);
        }

        private static string FindRepositoryRoot()
        {
            var candidates = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Directory.GetCurrentDirectory()
            };
            foreach (var candidate in candidates)
            {
                var current = new DirectoryInfo(candidate);
                while (current != null)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                        File.Exists(Path.Combine(current.FullName, ".git")) ||
                        File.Exists(Path.Combine(current.FullName, "IdeaCadConnector.sln")))
                        return current.FullName;
                    current = current.Parent;
                }
            }
            return null;
        }

        private static string Normalize(string path)
        {
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full + Path.DirectorySeparatorChar;
        }

        private static bool IsSameOrDescendant(string path, string root)
        {
            var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
