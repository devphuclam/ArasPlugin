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
        ICommand EditLibraryCommand { get; }
        ICommand ArchiveLibraryCommand { get; }
        ICommand ShowPartPickerCommand { get; }
        ICommand AddPartCommand { get; }
        ICommand RemoveEntryCommand { get; }
        ICommand MoveEntryCommand { get; }
        ICommand ShowRevisionBrowserCommand { get; }
        ICommand AddToCurrentProjectCommand { get; }
        ICommand OpenInIronCadCommand { get; }
        ICommand DownloadCadCommand { get; }
        ICommand PublishCommand { get; }
        ICommand DeprecateCommand { get; }
        ICommand PinRevisionCommand { get; }
        ICommand UseLatestReleasedCommand { get; }
        ICommand ViewWhereUsedCommand { get; }
        ICommand OpenInArasCommand { get; }

        string SelectedVisibilityFilter { get; set; }
        ObservableCollection<string> VisibilityFilters { get; }
        bool CanCreateLibrary { get; }
        bool CanEditSelectedLibrary { get; }
        bool CanArchiveSelectedLibrary { get; }
        bool CanAddEntryToSelectedLibrary { get; }
        bool CanUsePartPicker { get; }
    }

    public sealed class PartLibrarySummaryRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int ItemCount { get; set; }
        public string LibraryType { get; set; }
        public bool CanContribute { get; set; }
        public bool CanManage { get; set; }
        public bool IsArchived { get; set; }
        public string Description { get; set; }
        public bool IsPublic { get; set; }
        public string Status { get; set; }
        public string DefaultRevisionPolicy { get; set; }
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
        public string EntryStatus { get; set; }
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

        public string Generation { get; set; }
        public string PrimaryCadFileId { get; set; }
    }

    public sealed class CadDetailsView
    {
        public string PrimaryCadId { get; set; }
        public string PrimaryCadNumber { get; set; }
        public string PrimaryCadName { get; set; }
        public string PrimaryCadState { get; set; }
        public string FileId { get; set; }
        public string FileName { get; set; }
        public string FileVersion { get; set; }
        public string LockedBy { get; set; }
        public bool HasNative { get; set; }
        public string PartId { get; set; }
    }

    public sealed class BomDetailsView
    {
        public string EntryId { get; set; }
        public ObservableCollection<BomLineItemView> Items { get; set; }
    }

    public sealed class BomLineItemView
    {
        public string ComponentPartId { get; set; }
        public string ComponentPartNumber { get; set; }
        public string ComponentName { get; set; }
        public string ComponentRevision { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
    }

    public sealed class RevisionDetailsView
    {
        public string EntryId { get; set; }
        public string CurrentPartId { get; set; }
        public string CurrentRevision { get; set; }
        public string CurrentLifecycleState { get; set; }
        public string CurrentGeneration { get; set; }
        public ObservableCollection<RevisionHistoryItemView> Items { get; set; }
    }

    public sealed class RevisionHistoryItemView
    {
        public string PartId { get; set; }
        public string Revision { get; set; }
        public string Generation { get; set; }
        public string LifecycleState { get; set; }
        public string ModifiedOn { get; set; }
        public bool IsCurrent { get; set; }
    }

    public sealed class WhereUsedDetailsView
    {
        public string EntryId { get; set; }
        public ObservableCollection<WhereUsedItemView> Items { get; set; }
    }

    public sealed class WhereUsedItemView
    {
        public string ParentPartId { get; set; }
        public string ParentPartNumber { get; set; }
        public string ParentPartName { get; set; }
        public string ParentRevision { get; set; }
        public string ParentState { get; set; }
        public int Quantity { get; set; }
        public string Source { get; set; }
    }
}
