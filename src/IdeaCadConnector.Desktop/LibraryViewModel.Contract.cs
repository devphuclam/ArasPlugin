using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public interface ILibraryViewModel : INotifyPropertyChanged
    {
        ObservableCollection<PartLibrarySummaryRow> Libraries { get; }
        ObservableCollection<PartLibraryEntryRow> Entries { get; }

        PartLibrarySummaryRow SelectedLibrary { get; set; }
        PartLibraryEntryRow SelectedEntry { get; set; }
        PartLibraryEntryDetailsView SelectedEntryDetails { get; }

        string SearchText { get; set; }
        string SelectedTypeFilter { get; set; }
        string SelectedStateFilter { get; set; }
        string SelectedRevisionFilter { get; set; }

        bool IsLoading { get; }
        bool IsOffline { get; }
        bool HasActivePdmWorkspace { get; }
        string StatusMessage { get; }
        string ResultSummary { get; }
        string PagingSummary { get; }

        ICommand RefreshCommand { get; }
        ICommand SearchCommand { get; }
        ICommand CreateLibraryCommand { get; }
        ICommand AddPartCommand { get; }
        ICommand RemoveEntryCommand { get; }
        ICommand MoveEntryCommand { get; }
        ICommand AddToCurrentProjectCommand { get; }
        ICommand OpenInIronCadCommand { get; }
        ICommand DownloadCadCommand { get; }
        ICommand PublishCommand { get; }
        ICommand DeprecateCommand { get; }
        ICommand PinRevisionCommand { get; }
        ICommand UseLatestReleasedCommand { get; }
        ICommand ViewWhereUsedCommand { get; }
        ICommand OpenInArasCommand { get; }
    }

    public sealed class PartLibrarySummaryRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int ItemCount { get; set; }
        public string LibraryType { get; set; }
        public bool CanContribute { get; set; }
    }

    public sealed class PartLibraryEntryRow
    {
        public string EntryId { get; set; }
        public string LibraryId { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string PartType { get; set; }
        public string Revision { get; set; }
        public string LifecycleState { get; set; }
        public string EntryLifecycleState { get; set; }
        public string RevisionPolicy { get; set; }
        public string CadStatus { get; set; }
        public int UsageCount { get; set; }
        public bool HasNewerReleasedRevision { get; set; }
        public bool IsDeprecated { get; set; }
        public bool ResolutionFailed { get; set; }
        public string ResolutionError { get; set; }
        public bool CanAddToProject { get; set; }
        public string LibraryName { get; set; }
    }

    public sealed class PartLibraryEntryDetailsView
    {
        public string EntryId { get; set; }
        public string LibraryId { get; set; }
        public string LibraryName { get; set; }
        public string PartId { get; set; }
        public string PartConfigId { get; set; }
        public string PartNumber { get; set; }
        public string PartName { get; set; }
        public string PartType { get; set; }
        public string Revision { get; set; }
        public string LifecycleState { get; set; }
        public string EntryLifecycleState { get; set; }
        public string RevisionPolicy { get; set; }
        public string PrimaryCadId { get; set; }
        public string PrimaryCadFileName { get; set; }
        public string PrimaryCadState { get; set; }
        public string LockedBy { get; set; }
        public int UsageCount { get; set; }
        public string CadStatus { get; set; }
        public bool HasNewerReleasedRevision { get; set; }
        public bool ResolutionFailed { get; set; }
        public string ResolutionError { get; set; }
        public bool CanAddToProject { get; set; }
        public string WhereUsedSummary { get; set; }
    }
}
