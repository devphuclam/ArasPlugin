using System;
using System.Collections.Generic;
using System.IO;

namespace IdeaCadConnector.Core.Library
{
    public static class VaultFileValidator
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ics",
            ".icd",
            ".ic3d",
            ".ic2d",
            ".exb",
            ".sat",
            ".step",
            ".stp",
            ".x_t",
            ".x_b",
            ".dwg",
            ".dxf"
        };

        public static bool IsExtensionAllowed(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;
            var ext = extension.Trim();
            if (!ext.StartsWith("."))
                ext = "." + ext;
            return AllowedExtensions.Contains(ext);
        }

        public static string GetNormalizedExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrWhiteSpace(ext) ? ext.ToLowerInvariant() : null;
        }

        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            var name = Path.GetFileName(fileName);
            if (!string.Equals(name, fileName, StringComparison.Ordinal))
                return false;
            var ext = GetNormalizedExtension(fileName);
            return IsExtensionAllowed(ext);
        }

        public static bool ContainsPathTraversal(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var normalized = path.Replace('/', '\\');
            return normalized.Contains("..\\") ||
                   normalized.Contains("..") ||
                   normalized.StartsWith("\\\\", StringComparison.Ordinal) ||
                   normalized.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
        }
    }
}
