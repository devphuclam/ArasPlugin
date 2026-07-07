using System;

namespace IdeaCadConnector.Core.Library
{
    public sealed class VaultCacheKey : IEquatable<VaultCacheKey>
    {
        public string Server { get; set; }

        public string Database { get; set; }

        public string FileId { get; set; }

        public string RevisionGeneration { get; set; }

        public string UserName { get; set; }

        public string FileName { get; set; }

        public string Extension { get; set; }

        public string ToCacheFileName()
        {
            var ext = !string.IsNullOrWhiteSpace(Extension) ? Extension : ".cache";
            if (!ext.StartsWith("."))
                ext = "." + ext;
            var stem = $"{Server}_{Database}_{FileId}_{RevisionGeneration ?? "0"}";
            var safe = stem
                .Replace("://", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace("?", "_")
                .Replace("&", "_");
            return safe.TrimEnd('_') + ext;
        }

        public bool Equals(VaultCacheKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Server, other.Server, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Database, other.Database, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(FileId, other.FileId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(RevisionGeneration, other.RevisionGeneration, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(UserName, other.UserName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Extension, other.Extension, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is VaultCacheKey other && Equals(other));
        }

        public override int GetHashCode()
        {
            var hash = StringComparer.OrdinalIgnoreCase;
            return hash.GetHashCode(Server ?? "") ^
                   hash.GetHashCode(Database ?? "") ^
                   hash.GetHashCode(FileId ?? "") ^
                   hash.GetHashCode(RevisionGeneration ?? "") ^
                   hash.GetHashCode(UserName ?? "") ^
                   hash.GetHashCode(Extension ?? "");
        }
    }
}
