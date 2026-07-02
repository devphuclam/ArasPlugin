using System;
using IdeaCadConnector.Core.Contracts;

namespace IdeaCadConnector.Desktop.Services
{
    public interface IAppSessionContext
    {
        IPdmRepositoryClient PdmClient { get; set; }
        IArasCadClient ArasCadClient { get; set; }
        IPartLibraryClient PartLibraryClient { get; set; }
        string CurrentUserName { get; set; }
        PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }
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

        public string CurrentUserName { get; set; }

        public PdmProjectsViewModel CurrentPdmProjectsViewModel { get; set; }

        public bool IsConnected => PdmClient != null || ArasCadClient != null;
    }
}
