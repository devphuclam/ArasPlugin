using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace IdeaCadConnector.Workspace
{
    public sealed class DocumentFileIdentity
    {
        public string RelativePath { get; set; }
        public string AbsolutePath { get; set; }
        public string FileHash { get; set; }
        public long FileSize { get; set; }
        public bool IsAvailable { get; set; }
        public string FailureReason { get; set; }
    }

    public static class DocumentFileIdentityService
    {
        public static DocumentFileIdentity ResolveAndRead(
            string rootFolder,
            string sourcePath,
            string relativePath)
        {
            return ResolveAndRead(rootFolder, sourcePath, relativePath, File.OpenRead);
        }

        public static string ComputeSha256(string filePath)
        {
            return ResolveAndRead(null, filePath, filePath).FileHash;
        }

        public static DocumentFileIdentity ResolveAndRead(
            string rootFolder,
            string sourcePath,
            string relativePath,
            Func<string, Stream> openStream)
        {
            var stableRelativePath = NormalizeRelativePath(relativePath ?? sourcePath);
            var absolutePath = ResolveAbsolutePath(rootFolder, sourcePath, stableRelativePath);
            var identity = new DocumentFileIdentity
            {
                RelativePath = stableRelativePath,
                AbsolutePath = absolutePath,
                FileSize = 0,
                IsAvailable = false
            };

            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                identity.FailureReason = "Document file path is empty.";
                return identity;
            }

            try
            {
                using (var sha = SHA256.Create())
                using (var stream = openStream(absolutePath))
                {
                    var buffer = new byte[64 * 1024];
                    long size = 0;
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha.TransformBlock(buffer, 0, read, buffer, 0);
                        size += read;
                    }

                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    identity.FileHash = ToLowerHex(sha.Hash);
                    identity.FileSize = size;
                    identity.IsAvailable = true;
                }
            }
            catch (FileNotFoundException)
            {
                identity.FailureReason = "Document file not found.";
            }
            catch (DirectoryNotFoundException)
            {
                identity.FailureReason = "Document file directory not found.";
            }
            catch (UnauthorizedAccessException)
            {
                identity.FailureReason = "Document file is not readable.";
            }
            catch (IOException)
            {
                identity.FailureReason = "Document file is not readable.";
            }

            return identity;
        }

        private static string ResolveAbsolutePath(string rootFolder, string sourcePath, string relativePath)
        {
            var candidate = !string.IsNullOrWhiteSpace(sourcePath) && Path.IsPathRooted(sourcePath)
                ? sourcePath
                : Path.Combine(rootFolder ?? string.Empty, relativePath ?? string.Empty);

            return string.IsNullOrWhiteSpace(candidate) ? null : Path.GetFullPath(candidate);
        }

        private static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var parts = path.Replace('\\', '/').Split('/');
            var normalized = new List<string>();
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) || part == ".")
                    continue;
                if (part == "..")
                {
                    if (normalized.Count > 0)
                        normalized.RemoveAt(normalized.Count - 1);
                    continue;
                }
                normalized.Add(part);
            }

            return normalized.Count == 0 ? null : string.Join("/", normalized);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
