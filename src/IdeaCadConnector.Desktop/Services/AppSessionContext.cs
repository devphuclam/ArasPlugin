using System;
using IdeaCadConnector.Core.Contracts;

namespace IdeaCadConnector.Desktop.Services
{
    public interface IAppSessionContext
    {
        IPdmRepositoryClient PdmClient { get; set; }
        IArasCadClient ArasCadClient { get; set; }
        IPartLibraryClient PartLibraryClient { get; set; }
        string ArasServerUrl { get; set; }
        string ArasDatabase { get; set; }
        string IronCadExecutablePath { get; set; }
        string CurrentUserName { get; set; }
        PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }
        string PendingLibraryFocusLibraryId { get; set; }
        string PendingLibraryFocusEntryId { get; set; }
        event EventHandler LibraryDataChanged;
        event EventHandler LibraryWorkspaceRequested;
        void NotifyLibraryDataChanged();
        void RequestLibraryWorkspace();
        bool IsConnected { get; }
    }

    public sealed class AppSessionContext : IAppSessionContext
    {
        private static readonly AppSessionContext _current = new AppSessionContext();

        private AppSessionContext()
        {
        }

        public static AppSessionContext Current => _current;

        public IPdmRepositoryClient PdmClient { get; set; }

        public IArasCadClient ArasCadClient { get; set; }

        public IPartLibraryClient PartLibraryClient { get; set; }

        public string ArasServerUrl { get; set; }

        public string ArasDatabase { get; set; }

        public string IronCadExecutablePath { get; set; }

        public string CurrentUserName { get; set; }

        public PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }

        public string PendingLibraryFocusLibraryId { get; set; }

        public string PendingLibraryFocusEntryId { get; set; }

        public event EventHandler LibraryDataChanged;

        public event EventHandler LibraryWorkspaceRequested;

        public void NotifyLibraryDataChanged()
        {
            LibraryDataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RequestLibraryWorkspace()
        {
            LibraryWorkspaceRequested?.Invoke(this, EventArgs.Empty);
        }

        public bool IsConnected => PdmClient != null || ArasCadClient != null;
    }
}
