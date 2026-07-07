using IdeaCadConnector.Core.Errors;

namespace IdeaCadConnector.Core.Library
{
    public sealed class ArasOpenUrlResult
    {
        public bool Success { get; set; }

        public string Url { get; set; }

        public string ErrorMessage { get; set; }

        public ArasErrorCode? ErrorCode { get; set; }
    }
}
