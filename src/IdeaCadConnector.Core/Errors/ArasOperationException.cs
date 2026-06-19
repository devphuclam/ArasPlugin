using System;
using System.Collections.Generic;

namespace IdeaCadConnector.Core.Errors
{
    public sealed class ArasOperationException : Exception
    {
        public ArasOperationException(
            ArasErrorCode errorCode,
            string message,
            bool retryable = false,
            IDictionary<string, string> details = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Retryable = retryable;
            Details = details ?? new Dictionary<string, string>();
        }

        public ArasErrorCode ErrorCode { get; }

        public bool Retryable { get; }

        public IDictionary<string, string> Details { get; }
    }
}
