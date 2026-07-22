using System.Collections.Generic;

namespace IdeaCadConnector.Core.Cad
{
    /// <summary>
    /// Default backend-neutral reviewer provider used when no authoritative
    /// reviewer source is configured. It returns an empty list so that
    /// submit-for-review is blocked rather than defaulting to a fake user.
    /// </summary>
    public sealed class EmptyReviewerProvider : IReviewerProvider
    {
        public IReadOnlyList<string> GetReviewers() => System.Array.Empty<string>();
    }
}
