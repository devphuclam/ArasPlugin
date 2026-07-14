using System;
using System.Collections.Generic;
using System.IO;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public enum PdmOutputSafetyIssue { NotAbsolute, Missing, NotWritable, SourceOverlap, PackageExists, PathTraversal }

    public sealed class PdmOutputSafetyValidator
    {
        public IList<PdmOutputSafetyIssue> Validate(string outputFolder, string sourcePath, string packagePath)
        {
            var issues = new List<PdmOutputSafetyIssue>();
            if (string.IsNullOrWhiteSpace(outputFolder) || !Path.IsPathRooted(outputFolder)) { issues.Add(PdmOutputSafetyIssue.NotAbsolute); return issues; }
            var output = Path.GetFullPath(outputFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Directory.Exists(outputFolder)) issues.Add(PdmOutputSafetyIssue.Missing);
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                var source = Path.GetFullPath(sourcePath);
                var sourceRoot = Directory.Exists(source) ? source : Path.GetDirectoryName(source) + Path.DirectorySeparatorChar;
                var sourceRootBoundary = sourceRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.Equals(output, sourceRootBoundary, StringComparison.OrdinalIgnoreCase) ||
                    output.StartsWith(sourceRootBoundary, StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(output, StringComparison.OrdinalIgnoreCase) ||
                    sourceRoot.StartsWith(output, StringComparison.OrdinalIgnoreCase)) issues.Add(PdmOutputSafetyIssue.SourceOverlap);
            }
            if (!string.IsNullOrWhiteSpace(packagePath) && Directory.Exists(packagePath)) issues.Add(PdmOutputSafetyIssue.PackageExists);
            try
            {
                var probe = Path.Combine(outputFolder, ".pdm-write-probe-" + Guid.NewGuid().ToString("N"));
                using (File.Create(probe)) { }
                File.Delete(probe);
            }
            catch { issues.Add(PdmOutputSafetyIssue.NotWritable); }
            return issues;
        }
    }
}
