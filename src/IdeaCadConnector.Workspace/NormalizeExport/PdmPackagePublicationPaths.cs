using System;
using System.IO;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackagePublicationPaths
    {
        private PdmPackagePublicationPaths(string outputDirectory, string finalDirectory, string pendingDirectory)
        {
            OutputDirectory = outputDirectory;
            FinalDirectory = finalDirectory;
            PendingDirectory = pendingDirectory;
        }

        public string OutputDirectory { get; }

        public string FinalDirectory { get; }

        public string PendingDirectory { get; }

        public static PdmPackagePublicationPaths Create(string outputFolder, string projectCode, string nonce)
        {
            if (string.IsNullOrWhiteSpace(nonce) ||
                Path.IsPathRooted(nonce) ||
                nonce.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                nonce.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Nonce is required.", nameof(nonce));

            var normalizedProjectCode = PdmNameNormalizer.NormalizeProjectCode(projectCode);
            var outputDirectory = Path.GetFullPath(outputFolder ?? throw new ArgumentNullException(nameof(outputFolder)));
            var finalDirectory = Path.GetFullPath(Path.Combine(outputDirectory, normalizedProjectCode));
            var pendingDirectory = Path.GetFullPath(Path.Combine(
                outputDirectory, "." + normalizedProjectCode + ".pending-" + nonce));

            PdmPackagePublicationPathGuard.Validate(outputDirectory, pendingDirectory, finalDirectory);
            return new PdmPackagePublicationPaths(outputDirectory, finalDirectory, pendingDirectory);
        }
    }

    internal static class PdmPackagePublicationPathGuard
    {
        public static void Validate(string outputDirectory, string pendingDirectory, string finalDirectory)
        {
            try
            {
                var output = NormalizeDirectory(outputDirectory);
                var pending = Path.GetFullPath(pendingDirectory ?? throw new ArgumentNullException(nameof(pendingDirectory)));
                var final = Path.GetFullPath(finalDirectory ?? throw new ArgumentNullException(nameof(finalDirectory)));
                if (!IsDirectChild(pending, output) ||
                    !IsDirectChild(final, output) ||
                    string.Equals(pending, final, StringComparison.OrdinalIgnoreCase) ||
                    HasExistingReparseComponent(output) ||
                    HasExistingReparseComponent(pending) ||
                    HasExistingReparseComponent(final))
                    throw UnsafePath();
            }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is NotSupportedException ||
                                       ex is PathTooLongException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is IOException)
            {
                throw UnsafePath(ex);
            }
        }

        public static void ValidateRecursiveDelete(string outputDirectory, string path)
        {
            try
            {
                var output = NormalizeDirectory(outputDirectory);
                var target = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
                if (!IsDirectChild(target, output) ||
                    HasExistingReparseComponent(output) ||
                    HasExistingReparseComponent(target))
                    throw UnsafePath();
            }
            catch (PdmNormalizeExportException) { throw; }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is NotSupportedException ||
                                       ex is PathTooLongException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is IOException)
            {
                throw UnsafePath(ex);
            }
        }

        private static string NormalizeDirectory(string path)
        {
            var full = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            var root = Path.GetPathRoot(full);
            return full.Length == root.Length
                ? full
                : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsDirectChild(string path, string parent)
        {
            var childParent = Path.GetDirectoryName(
                path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return !string.IsNullOrWhiteSpace(childParent) &&
                string.Equals(NormalizeDirectory(childParent), parent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasExistingReparseComponent(string path)
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)) return true;

            var current = root;
            if (HasReparsePoint(current)) return true;
            foreach (var segment in full.Substring(root.Length).Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (!Directory.Exists(current) && !File.Exists(current)) break;
                if (HasReparsePoint(current)) return true;
            }
            return false;
        }

        private static bool HasReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static PdmNormalizeExportException UnsafePath(Exception inner = null)
        {
            return new PdmNormalizeExportException(
                "PACKAGE_PATH_UNSAFE",
                "Package publication paths are unsafe.",
                inner == null ? null : inner.ToString(),
                inner);
        }
    }
}
