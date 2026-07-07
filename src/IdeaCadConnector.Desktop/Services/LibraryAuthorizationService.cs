using IdeaCadConnector.Core.Library;
using System;

namespace IdeaCadConnector.Desktop.Services
{
    internal sealed class LibraryAuthorizationService : ILibraryAuthorizationService
    {
        private readonly IAppSessionContext _session;
        private readonly LibraryAuthorizationRules _rules;

        public LibraryAuthorizationService(IAppSessionContext session)
            : this(session, null)
        {
        }

        internal LibraryAuthorizationService(IAppSessionContext session, LibraryAuthorizationRules rules)
        {
            _session = session ?? throw new System.ArgumentNullException(nameof(session));
            _rules = rules ?? LibraryAuthorizationRules.Default;
        }

        public bool IsLibraryManager
        {
            get
            {
                var user = Normalize(_session.CurrentUserName);
                return _rules.IsManager(user);
            }
        }

        public bool IsContributorOrHigher
        {
            get
            {
                if (IsLibraryManager)
                    return true;

                var user = Normalize(_session.CurrentUserName);
                return _rules.IsContributorOrHigher(user);
            }
        }

        public bool IsReviewerOrHigher
        {
            get
            {
                if (IsLibraryManager)
                    return true;

                var user = Normalize(_session.CurrentUserName);
                return _rules.IsReviewer(user);
            }
        }

        public bool IsReadOnlyViewer => !IsContributorOrHigher && !IsLibraryManager;

        public bool CanManageLibraries => _session.IsConnected && IsLibraryManager;

        public bool CanUsePartPicker => _session.IsConnected && IsContributorOrHigher;

        public bool CanMoveEntries => _session.IsConnected && IsReviewerOrHigher;

        public bool CanPinRevisions => _session.IsConnected && IsReviewerOrHigher;

        private static string Normalize(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();
        }
    }
}
