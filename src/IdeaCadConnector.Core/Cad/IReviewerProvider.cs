using System.Collections.Generic;

namespace IdeaCadConnector.Core.Cad
{
    /// <summary>
    /// Backend-neutral source of reviewers eligible for a CAD review assignment.
    /// Implementations must return authoritative reviewer identities only; they
    /// must never fabricate or default to placeholder users. When no authoritative
    /// source is configured, return an empty list so the caller can block the
    /// submit-for-review action with a clear message.
    /// </summary>
    public interface IReviewerProvider
    {
        /// <summary>
        /// Returns the reviewers available for assignment, or an empty list when
        /// no authoritative source is available. Never returns placeholder identities.
        /// </summary>
        IReadOnlyList<string> GetReviewers();
    }
}
