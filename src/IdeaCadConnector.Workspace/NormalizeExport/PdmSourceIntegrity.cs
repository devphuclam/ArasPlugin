using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace IdeaCadConnector.Workspace.NormalizeExport
{
    public sealed class PdmSourceFileFingerprint
    {
        public string Path { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public string Sha256 { get; set; }
    }

    public static class PdmSourceIntegrity
    {
        public static PdmSourceFileFingerprint Capture(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("SOURCE_FILE_MISSING");
            var full = System.IO.Path.GetFullPath(path);
            using (var stream = File.OpenRead(full))
            using (var sha = SHA256.Create())
            {
                return new PdmSourceFileFingerprint
                {
                    Path = full,
                    Length = stream.Length,
                    LastWriteUtc = File.GetLastWriteTimeUtc(full),
                    Sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty)
                };
            }
        }

        public static bool Matches(PdmSourceFileFingerprint fingerprint)
        {
            if (fingerprint == null || !File.Exists(fingerprint.Path)) return false;
            var current = Capture(fingerprint.Path);
            return current.Length == fingerprint.Length &&
                string.Equals(current.Sha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
