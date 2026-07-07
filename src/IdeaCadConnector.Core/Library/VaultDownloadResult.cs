using IdeaCadConnector.Core.Errors;

namespace IdeaCadConnector.Core.Library
{
    public sealed class VaultDownloadResult
    {
        public bool Success { get; set; }

        public string LocalFilePath { get; set; }

        public string ErrorMessage { get; set; }

        public ArasErrorCode? ErrorCode { get; set; }

        public string FileId { get; set; }

        public string FileName { get; set; }

        public long BytesWritten { get; set; }

        public bool FromCache { get; set; }

        public VaultCacheKey CacheKey { get; set; }
    }
}
