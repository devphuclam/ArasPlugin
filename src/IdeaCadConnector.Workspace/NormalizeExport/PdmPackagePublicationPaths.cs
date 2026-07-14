using System;
using System.IO;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmPackagePublicationPaths
    {
        private PdmPackagePublicationPaths(string finalDirectory, string pendingDirectory)
        {
            FinalDirectory = finalDirectory;
            PendingDirectory = pendingDirectory;
        }

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
            var finalDirectory = Path.GetFullPath(Path.Combine(outputFolder, normalizedProjectCode));
            var pendingDirectory = Path.GetFullPath(Path.Combine(
                outputFolder, "." + normalizedProjectCode + ".pending-" + nonce));

            return new PdmPackagePublicationPaths(finalDirectory, pendingDirectory);
        }
    }
}
