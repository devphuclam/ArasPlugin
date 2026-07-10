using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Workspace;
namespace IdeaCadConnector.Desktop
{
    public sealed class LibraryViewModel : ILibraryViewModel
    {
        private const string NoValidTargetParentMessage = TranslationKeys.LibraryErrorNoParentPart;
        private readonly IAppSessionContext _session;
        private readonly IPartLibraryClient _injectedClient;
        private readonly IPartLibraryClient _unavailableClient = new UnavailablePartLibraryClient();
        private readonly ILibraryAuthorizationService _authService;
        private readonly IPartLibraryVaultService _injectedVaultService;
        private readonly IIronCadOpenService _injectedIronCadService;
        private readonly IArasOpenUrlService _injectedOpenUrlService;
        private readonly IBrowserLauncher _injectedBrowserLauncher;
        private IPartLibraryVaultService _vaultService;
        private IIronCadOpenService _ironCadService;
        private IArasOpenUrlService _openUrlService;
        private IBrowserLauncher _browserLauncher;
        private PartLibrarySummaryRow _selectedLibrary;
        private PartLibraryEntryRow _selectedEntry;
        private PartLibraryEntryDetailsView _selectedEntryDetails;
        private string _searchText;
        private string _selectedTypeFilter;
        private string _selectedStateFilter;
        private string _selectedRevisionFilter;
        private bool _isLoading;
        private bool _isAddingToCurrentProject;
        private string _statusMessage;
        private string _errorMessage;
        private string _permissionMessage;
        private int _totalCount;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private string _selectedVisibilityFilter;
        private string _selectedEntryStatusFilter;
        private string _selectedCadStatusFilter;
        private string _selectedSortOption;
        private string _selectedSortDirection;
        private LibraryVisibilityFilter _activeVisibilityFilter = LibraryVisibilityFilter.Active;
        private CadDetailsView _cadDetails;
        private BomDetailsView _bomDetails;
        private RevisionDetailsView _revisionDetails;
        private WhereUsedDetailsView _whereUsedDetails;
        private bool _isLoadingDetails;
        private string _detailStatusMessage;
        private string _detailErrorMessage;
        private string _lastLoadedDetailEntryId;

        public LibraryViewModel()
            : this(AppSessionContext.Current, null, null, null, null, null, null)
        {
        }

        public LibraryViewModel(IAppSessionContext session, IPartLibraryClient client)
            : this(session, client, null, null, null, null, null)
        {
        }

        internal LibraryViewModel(
            IAppSessionContext session,
            IPartLibraryClient client,
            ILibraryAuthorizationService authService,
            IPartLibraryVaultService vaultService = null,
            IIronCadOpenService ironCadService = null,
            IArasOpenUrlService openUrlService = null,
            IBrowserLauncher browserLauncher = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _injectedClient = client;
            _authService = authService ?? new LibraryAuthorizationService(session);
            _injectedVaultService = vaultService;
            _injectedIronCadService = ironCadService;
            _injectedOpenUrlService = openUrlService;
            _injectedBrowserLauncher = browserLauncher;

            Libraries = new ObservableCollection<PartLibrarySummaryRow>();
            Entries = new ObservableCollection<PartLibraryEntryRow>();
            TypeFilters = new ObservableCollection<string>();
            StateFilters = new ObservableCollection<string>();
            RevisionFilters = new ObservableCollection<string>();
            VisibilityFilters = new ObservableCollection<string>();
            RefreshFilterOptions();
            RefreshVisibilityFilterOptions();

            _selectedTypeFilter = TypeFilters[0];
            _selectedStateFilter = StateFilters[0];
            _selectedRevisionFilter = RevisionFilters[0];
            _selectedVisibilityFilter = VisibilityFilters[0];
            EntryStatusFilters = new ObservableCollection<string>();
            CadStatusFilters = new ObservableCollection<string>();
            SortOptions = new ObservableCollection<string>();
            SortDirections = new ObservableCollection<string>();
            RefreshEntryStatusFilterOptions();
            RefreshCadStatusFilterOptions();
            RefreshSortOptions();
            _selectedEntryStatusFilter = EntryStatusFilters[0];
            _selectedCadStatusFilter = CadStatusFilters[0];
            _selectedSortOption = SortOptions[0];
            _selectedSortDirection = SortDirections[0];
            _selectedEntryDetails = CreateEmptyDetails();
            _statusMessage = L(TranslationKeys.LibraryStatusReady);
            _permissionMessage = string.Empty;
            _errorMessage = string.Empty;
            _cadDetails = new CadDetailsView();
            _bomDetails = new BomDetailsView { Items = new ObservableCollection<BomLineItemView>() };
            _revisionDetails = new RevisionDetailsView { Items = new ObservableCollection<RevisionHistoryItemView>() };
            _whereUsedDetails = new WhereUsedDetailsView { Items = new ObservableCollection<WhereUsedItemView>() };

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsLoading);
            SearchCommand = new RelayCommand(_ => _ = SearchAsync(), _ => !IsLoading);
            CreateLibraryCommand = new RelayCommand(_ => _ = ShowCreateLibraryDialogAsync(), _ => CanCreateLibrary);
            EditLibraryCommand = new RelayCommand(_ => _ = ShowEditLibraryDialogAsync(), _ => !IsLoading && !IsOffline && CanEditSelectedLibrary);
            ArchiveLibraryCommand = new RelayCommand(_ => _ = ShowArchiveLibraryFlowAsync(), _ => !IsLoading && !IsOffline && CanArchiveSelectedLibrary);
            ShowPartPickerCommand = new RelayCommand(_ => _ = ShowArasPartPickerDialogAsync(), _ => CanUsePartPickerForSelectedLibrary);
            AddPartCommand = new RelayCommand(_ => ShowSaveToLibraryDialog(), _ => !IsLoading && !IsOffline && CanAddEntryToSelectedLibrary);
            RemoveEntryCommand = new RelayCommand(_ => _ = RemoveSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading);
            MoveEntryCommand = new RelayCommand(_ => _ = MoveSelectedEntryAsync(), _ => CanExecuteMoveEntry());
            ShowRevisionBrowserCommand = new RelayCommand(_ => _ = ShowRevisionBrowserDialogAsync(), _ => CanExecuteShowRevisionBrowser());
            AddToCurrentProjectCommand = new RelayCommand(_ => _ = AddToCurrentProjectAsync(), _ => CanAddToCurrentProject());
            OpenInIronCadCommand = new RelayCommand(_ => _ = OpenPrimaryCadAsync(), _ => SelectedEntry != null && !IsLoading);
            DownloadCadCommand = new RelayCommand(_ => _ = DownloadCadAsync(), _ => SelectedEntry != null && !IsLoading);
            PublishCommand = new RelayCommand(_ => _ = PublishSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading && !SelectedEntry.IsDeprecated);
            DeprecateCommand = new RelayCommand(_ => _ = DeprecateSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading && !SelectedEntry.IsDeprecated);
            PinRevisionCommand = new RelayCommand(_ => _ = ResolveSelectedEntryAsync(LibraryRevisionPolicy.Pinned), _ => SelectedEntry != null && !IsLoading);
            UseLatestReleasedCommand = new RelayCommand(_ => _ = ResolveSelectedEntryAsync(LibraryRevisionPolicy.LatestReleased), _ => SelectedEntry != null && !IsLoading);
            ViewWhereUsedCommand = new RelayCommand(_ => _ = ViewWhereUsedAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenSelectedPartInArasCommand = new RelayCommand(_ => _ = OpenSelectedPartInArasAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenSelectedEntryInArasCommand = new RelayCommand(_ => _ = OpenSelectedEntryInArasAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenSelectedLibraryInArasCommand = new RelayCommand(_ => _ = OpenSelectedLibraryInArasAsync(), _ => SelectedLibrary != null && !IsLoading);
            OpenSelectedCadInArasCommand = new RelayCommand(_ => _ = OpenSelectedCadInArasAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenInArasCommand = OpenSelectedPartInArasCommand;

            LocalizationSource.Instance.PropertyChanged += OnLocalizationChanged;
            _session.LibraryDataChanged += OnLibraryDataChanged;
            AddToCurrentProjectDialogHandler = ShowAddToCurrentProjectDialog;
            AddLibraryReferenceHandler = (workspace, reference) => workspace.AddLibraryReference(reference);
            CreateLibraryDialogHandler = ShowCreateLibraryDialog;
            EditLibraryDialogHandler = ShowEditLibraryDialog;
            PartPickerDialogHandler = ShowPartPickerDialog;
            MoveEntryDialogHandler = ShowMoveEntryDialog;
            RevisionBrowserDialogHandler = ShowRevisionBrowserDialog;
            ConfirmDialogHandler = (message, caption, buttons, image) => MessageBox.Show(message, caption, buttons, image);

            RefreshLibraryServices();
            _ = RefreshAsync();
        }

        private void RefreshLibraryServices()
        {
            var composedServices = LibraryServicesFactory.Create(_session, _injectedClient);

            _vaultService = _injectedVaultService ?? composedServices.VaultService;
            _ironCadService = _injectedIronCadService ?? composedServices.IronCadOpenService;
            _openUrlService = _injectedOpenUrlService ?? composedServices.ArasOpenUrlService;
            _browserLauncher = _injectedBrowserLauncher ?? composedServices.BrowserLauncher;
        }

        private IPartLibraryClient ActiveClient =>
            _injectedClient
            ?? _session.PartLibraryClient
            ?? _unavailableClient;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<PartLibrarySummaryRow> Libraries { get; }

        public ObservableCollection<PartLibraryEntryRow> Entries { get; }

        public ObservableCollection<string> TypeFilters { get; }

        public ObservableCollection<string> StateFilters { get; }

        public ObservableCollection<string> RevisionFilters { get; }

        public ObservableCollection<string> VisibilityFilters { get; }

        public ObservableCollection<string> EntryStatusFilters { get; }

        public ObservableCollection<string> CadStatusFilters { get; }

        public ObservableCollection<string> SortOptions { get; }

        public ObservableCollection<string> SortDirections { get; }

        public string SelectedVisibilityFilter
        {
            get => _selectedVisibilityFilter;
            set
            {
                if (SetField(ref _selectedVisibilityFilter, value))
                {
                    if (value == L(TranslationKeys.LibraryFilterActive))
                        _activeVisibilityFilter = LibraryVisibilityFilter.Active;
                    else if (value == L(TranslationKeys.LibraryFilterArchived))
                        _activeVisibilityFilter = LibraryVisibilityFilter.Archived;
                    else
                        _activeVisibilityFilter = LibraryVisibilityFilter.All;

                    _ = RefreshAsync();
                }
            }
        }

        public bool IsLibraryManager => _authService?.IsLibraryManager ?? false;

        public bool IsContributorOrHigher => _authService?.IsContributorOrHigher ?? false;

        public bool IsReadOnlyViewer => _authService?.IsReadOnlyViewer ?? true;

        public bool CanManageLibraries => _authService?.CanManageLibraries ?? false;

        public bool CanCreateLibrary => !IsLoading && !IsOffline && CanManageLibraries;

        public bool CanUsePartPicker => !IsLoading && !IsOffline && (_authService?.CanUsePartPicker ?? false);

        public bool CanEditSelectedLibrary =>
            SelectedLibrary != null &&
            CanManageLibraries &&
            !SelectedLibrary.IsArchived;

        public bool CanArchiveSelectedLibrary =>
            SelectedLibrary != null &&
            CanManageLibraries &&
            !SelectedLibrary.IsArchived;

        public bool CanAddEntryToSelectedLibrary =>
            SelectedLibrary != null &&
            !SelectedLibrary.IsArchived &&
            CanUsePartPicker;

        public bool CanUsePartPickerForSelectedLibrary => CanAddEntryToSelectedLibrary;

        public PartLibrarySummaryRow SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetField(ref _selectedLibrary, value))
                {
                    OnPropertyChanged(nameof(CanContributeToSelectedLibrary));
                    OnPropertyChanged(nameof(CanAddEntryToSelectedLibrary));
                    OnPropertyChanged(nameof(CanUsePartPickerForSelectedLibrary));
                    OnPropertyChanged(nameof(CanEditSelectedLibrary));
                    OnPropertyChanged(nameof(CanArchiveSelectedLibrary));
                    NotifyPanelStateChanged();
                    _ = SearchAsync();
                    RaiseCommandStates();
                }
            }
        }

        public PartLibraryEntryRow SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (SetField(ref _selectedEntry, value))
                {
                    _ = LoadSelectedEntryAsync();
                    RaiseCommandStates();
                }
            }
        }

        public PartLibraryEntryDetailsView SelectedEntryDetails
        {
            get => _selectedEntryDetails;
            private set => SetField(ref _selectedEntryDetails, value);
        }

        public CadDetailsView SelectedCadDetails
        {
            get => _cadDetails;
            private set => SetField(ref _cadDetails, value);
        }

        public BomDetailsView SelectedBomDetails
        {
            get => _bomDetails;
            private set => SetField(ref _bomDetails, value);
        }

        public RevisionDetailsView SelectedRevisionDetails
        {
            get => _revisionDetails;
            private set => SetField(ref _revisionDetails, value);
        }

        public WhereUsedDetailsView SelectedWhereUsedDetails
        {
            get => _whereUsedDetails;
            private set => SetField(ref _whereUsedDetails, value);
        }

        public bool IsLoadingDetails
        {
            get => _isLoadingDetails;
            private set
            {
                if (SetField(ref _isLoadingDetails, value))
            NotifyDetailStateChanged();
            OnPropertyChanged(nameof(HasSortDirections));
        }
        }

        public string DetailStatusMessage
        {
            get => _detailStatusMessage;
            private set => SetField(ref _detailStatusMessage, value);
        }

        public string DetailErrorMessage
        {
            get => _detailErrorMessage;
            private set => SetField(ref _detailErrorMessage, value);
        }

        public bool HasCadDetails => SelectedCadDetails != null && !string.IsNullOrWhiteSpace(SelectedCadDetails.PrimaryCadId);

        public bool HasNoCadDetails => !HasCadDetails && !IsLoadingDetails;

        public bool HasBomItems => SelectedBomDetails?.Items != null && SelectedBomDetails.Items.Count > 0;

        public bool HasNoBomItems => !HasBomItems && !IsLoadingDetails;

        public bool HasRevisionItems => SelectedRevisionDetails?.Items != null && SelectedRevisionDetails.Items.Count > 0;

        public bool HasNoRevisionItems => !HasRevisionItems && !IsLoadingDetails;

        public bool HasWhereUsedItems => SelectedWhereUsedDetails?.Items != null && SelectedWhereUsedDetails.Items.Count > 0;

        public bool HasNoWhereUsedItems => !HasWhereUsedItems && !IsLoadingDetails;

        public string SearchText
        {
            get => _searchText;
            set => SetField(ref _searchText, value);
        }

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set => SetField(ref _selectedTypeFilter, value);
        }

        public string SelectedStateFilter
        {
            get => _selectedStateFilter;
            set => SetField(ref _selectedStateFilter, value);
        }

        public string SelectedRevisionFilter
        {
            get => _selectedRevisionFilter;
            set => SetField(ref _selectedRevisionFilter, value);
        }

        public string SelectedEntryStatusFilter
        {
            get => _selectedEntryStatusFilter;
            set
            {
                if (SetField(ref _selectedEntryStatusFilter, value))
                    _ = SearchAsync();
            }
        }

        public string SelectedCadStatusFilter
        {
            get => _selectedCadStatusFilter;
            set
            {
                if (SetField(ref _selectedCadStatusFilter, value))
                    _ = SearchAsync();
            }
        }

        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetField(ref _selectedSortOption, value))
                    _ = SearchAsync();
            }
        }

        public string SelectedSortDirection
        {
            get => _selectedSortDirection;
            set
            {
                if (SetField(ref _selectedSortDirection, value))
                    _ = SearchAsync();
            }
        }

        public bool HasSortDirections => SortDirections.Count > 0;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetField(ref _isLoading, value))
                    RaiseCommandStates();
            }
        }

        public bool IsOffline => !_session.IsConnected;

        public bool HasActivePdmWorkspace => _session.CurrentPdmProjectsViewModel != null;

        internal Func<AddLibraryPartToProjectDialogViewModel, bool?> AddToCurrentProjectDialogHandler { get; set; }

        internal Func<PdmProjectsViewModel, WorkspaceLibraryReference, LibraryReferenceMutationResult> AddLibraryReferenceHandler { get; set; }

        internal Func<CreateLibraryDialogViewModel, bool?> CreateLibraryDialogHandler { get; set; }

        internal Func<EditLibraryDialogViewModel, bool?> EditLibraryDialogHandler { get; set; }

        internal Func<ArasPartPickerViewModel, bool?> PartPickerDialogHandler { get; set; }

        internal Func<MoveLibraryEntryDialogViewModel, bool?> MoveEntryDialogHandler { get; set; }

        internal Func<PartRevisionBrowserViewModel, bool?> RevisionBrowserDialogHandler { get; set; }

        internal Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> ConfirmDialogHandler { get; set; }

        public bool CanContributeToSelectedLibrary => CanAddEntryToSelectedLibrary;

        public bool IsPermissionState => !string.IsNullOrWhiteSpace(_permissionMessage);

        public bool IsErrorState => !string.IsNullOrWhiteSpace(_errorMessage);

        public bool ShowLibrariesOverlay => IsLoading || IsOffline || IsPermissionState || IsErrorState || Libraries.Count == 0;

        public bool ShowEntriesOverlay => IsLoading || IsOffline || IsPermissionState || IsErrorState || Entries.Count == 0;

        public string LibrariesOverlayMessage
        {
            get
            {
                if (IsLoading)
                    return L(TranslationKeys.LibraryOverlayLoadingLibraries);
                if (IsOffline)
                    return L(TranslationKeys.LibraryOverlaySignInLibraries);
                if (IsPermissionState)
                    return _permissionMessage;
                if (IsErrorState)
                    return _errorMessage;
                if (Libraries.Count == 0)
                    return L(TranslationKeys.LibraryOverlayNoAccessibleLibraries);
                return string.Empty;
            }
        }

        public string EntriesOverlayMessage
        {
            get
            {
                if (IsLoading)
                    return L(TranslationKeys.LibraryOverlayLoadingEntries);
                if (IsOffline)
                    return L(TranslationKeys.LibraryOverlaySignInEntries);
                if (IsPermissionState)
                    return _permissionMessage;
                if (IsErrorState)
                    return _errorMessage;
                if (Libraries.Count == 0)
                    return L(TranslationKeys.LibraryOverlaySelectLibraryAfterSignIn);
                if (Entries.Count > 0)
                    return string.Empty;
                return L(TranslationKeys.LibraryOverlayNoMatchedEntries);
            }
        }

        public string AddToProjectHint => HasActivePdmWorkspace
            ? string.Empty
            : L(TranslationKeys.LibraryHintOpenPdmWorkspace);

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public string ResultSummary => _totalCount <= 0
            ? L(TranslationKeys.LibraryResultSummaryNone)
            : Lf(TranslationKeys.LibraryResultSummarySome, _totalCount);

        public string PagingSummary
        {
            get
            {
                if (_totalCount <= 0)
                    return L(TranslationKeys.LibraryPagingSummaryZero);

                var start = ((_pageNumber - 1) * _pageSize) + 1;
                var end = Math.Min(_totalCount, _pageNumber * _pageSize);
                return Lf(TranslationKeys.LibraryPagingSummaryRange, start, end, _totalCount);
            }
        }

        public string ConnectionTitle => _session.IsConnected
            ? Lf(TranslationKeys.LibraryConnectionConnectedAs, _session.CurrentUserName ?? L(TranslationKeys.DisplayEngineer))
            : L(TranslationKeys.LibraryConnectionOffline);

        public string ConnectionDatabase => _session.IsConnected
            ? L(TranslationKeys.LibraryConnectionWorkspaceActive)
            : L(TranslationKeys.LibraryConnectionConnectHint);

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand CreateLibraryCommand { get; }
        public ICommand AddPartCommand { get; }
        public ICommand RemoveEntryCommand { get; }
        public ICommand MoveEntryCommand { get; }
        public ICommand ShowRevisionBrowserCommand { get; }
        public ICommand AddToCurrentProjectCommand { get; }
        public ICommand OpenInIronCadCommand { get; }
        public ICommand DownloadCadCommand { get; }
        public ICommand PublishCommand { get; }
        public ICommand DeprecateCommand { get; }
        public ICommand PinRevisionCommand { get; }
        public ICommand UseLatestReleasedCommand { get; }
        public ICommand ViewWhereUsedCommand { get; }
        public ICommand OpenSelectedPartInArasCommand { get; }
        public ICommand OpenSelectedEntryInArasCommand { get; }
        public ICommand OpenSelectedLibraryInArasCommand { get; }
        public ICommand OpenSelectedCadInArasCommand { get; }
        public ICommand OpenInArasCommand { get; }

        public ICommand EditLibraryCommand { get; }
        public ICommand ArchiveLibraryCommand { get; }
        public ICommand ShowPartPickerCommand { get; }

        private async Task RefreshAsync()
        {
            RefreshLibraryServices();
            ClearTransientStates();
            if (IsOffline)
            {
                Libraries.Clear();
                Entries.Clear();
                SelectedLibrary = null;
                SelectedEntry = null;
                SelectedEntryDetails = CreateEmptyDetails();
                _totalCount = 0;
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(PagingSummary));
                NotifyPanelStateChanged();
                StatusMessage = L(TranslationKeys.LibraryStatusOffline);
                return;
            }

            await RunBusyAsync(async () =>
            {
                var libraries = await ActiveClient.GetLibrariesAsync(_activeVisibilityFilter, CancellationToken.None).ConfigureAwait(true);
                Libraries.Clear();
                foreach (var library in libraries)
                {
                    Libraries.Add(new PartLibrarySummaryRow
                    {
                        Id = library.Id,
                        Name = library.Name,
                        ItemCount = library.ItemCount,
                        LibraryType = library.LibraryType.ToString(),
                        CanContribute = library.CanContribute,
                        CanManage = CanManageLibraries,
                        IsArchived = string.Equals(library.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase),
                        Description = library.Description,
                        IsPublic = library.IsPublic,
                        Status = library.Status,
                        DefaultRevisionPolicy = library.DefaultRevisionPolicy
                    });
                }

                if (Libraries.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(_session.PendingLibraryFocusLibraryId))
                    {
                        var preferredLibrary = Libraries.FirstOrDefault(item =>
                            string.Equals(item.Id, _session.PendingLibraryFocusLibraryId, StringComparison.OrdinalIgnoreCase));
                        SelectedLibrary = preferredLibrary ?? Libraries[0];
                    }
                    else
                    {
                        SelectedLibrary = Libraries[0];
                    }
                }
                else
                    await SearchAsync().ConfigureAwait(true);

                StatusMessage = Libraries.Count > 0
                    ? L(TranslationKeys.LibraryStatusDataRefreshed)
                    : L(TranslationKeys.LibraryOverlayNoAccessibleLibraries);
                NotifyPanelStateChanged();
            });
        }

        private async Task SearchAsync()
        {
            ClearTransientStates();
            if (IsOffline)
            {
                Entries.Clear();
                SelectedEntry = null;
                SelectedEntryDetails = new PartLibraryEntryDetailsView();
                _totalCount = 0;
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(PagingSummary));
                NotifyPanelStateChanged();
                StatusMessage = L(TranslationKeys.LibraryStatusSearchOffline);
                return;
            }

            await RunBusyAsync(async () =>
            {
                var response = await ActiveClient.SearchEntriesAsync(new PartLibrarySearchRequest
                {
                    LibraryId = SelectedLibrary?.Id,
                    SearchText = SearchText,
                    TypeFilter = NormalizeTypeFilter(SelectedTypeFilter),
                    StateFilter = NormalizeStateFilter(SelectedStateFilter),
                    RevisionFilter = NormalizeRevisionFilter(SelectedRevisionFilter),
                    PageNumber = _pageNumber,
                    PageSize = _pageSize
                }, CancellationToken.None).ConfigureAwait(true);

                Entries.Clear();
                foreach (var entry in response.Entries ?? Array.Empty<PartLibraryEntrySummary>())
                {
                    Entries.Add(MapEntry(entry));
                }

                // Apply local entry status filter
                var normalizedEntryStatusFilter = NormalizeEntryStatusFilter(SelectedEntryStatusFilter);
                if (!string.IsNullOrWhiteSpace(normalizedEntryStatusFilter))
                {
                    var filtered = Entries.Where(e => string.Equals(e.EntryStatus, normalizedEntryStatusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                    Entries.Clear();
                    foreach (var entry in filtered)
                        Entries.Add(entry);
                }

                // Apply local CAD status filter
                var normalizedCadStatusFilter = NormalizeCadStatusFilter(SelectedCadStatusFilter);
                if (!string.IsNullOrWhiteSpace(normalizedCadStatusFilter))
                {
                    var filtered = Entries.Where(e => string.Equals(e.CadStatus, normalizedCadStatusFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                    Entries.Clear();
                    foreach (var entry in filtered)
                        Entries.Add(entry);
                }

                // Apply local sorting
                var sorted = SortEntries(Entries, SelectedSortOption, SelectedSortDirection).ToList();
                Entries.Clear();
                foreach (var entry in sorted)
                    Entries.Add(entry);

                _totalCount = Entries.Count;
                _pageNumber = response.PageNumber <= 0 ? 1 : response.PageNumber;
                _pageSize = response.PageSize <= 0 ? 25 : response.PageSize;
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(PagingSummary));

                if (Entries.Count > 0)
                {
                    SelectedEntry = Entries[0];
                    TryFocusPendingEntry();
                }
                else
                    SelectedEntryDetails = CreateEmptyDetails();

                StatusMessage = Entries.Count > 0
                    ? L(TranslationKeys.LibraryStatusResultsUpdated)
                    : L(TranslationKeys.LibraryOverlayNoMatchedEntries);
                NotifyPanelStateChanged();
            });
        }

        private async Task LoadSelectedEntryAsync()
        {
            if (SelectedEntry == null)
            {
                SelectedEntryDetails = CreateEmptyDetails();
                ClearDetailTabs();
                return;
            }

            var entryId = SelectedEntry.EntryId;
            ClearDetailTabs();
            var details = await ActiveClient.GetEntryAsync(entryId, CancellationToken.None).ConfigureAwait(true);
            SelectedEntryDetails = new PartLibraryEntryDetailsView
            {
                EntryId = details.EntryId,
                LibraryId = details.LibraryId,
                LibraryName = details.LibraryName,
                PartId = details.PartId,
                PartConfigId = details.PartConfigId,
                PartNumber = details.PartNumber,
                PartName = details.PartName,
                PartType = details.PartType,
                Revision = details.Revision,
                Generation = details.Generation,
                LifecycleState = details.LifecycleState,
                EntryLifecycleState = details.EntryLifecycleState,
                RevisionPolicy = details.RevisionPolicy.ToString(),
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
                PrimaryCadFileId = details.PrimaryCadFileId,
                LockedBy = details.LockedBy,
                UsageCount = details.UsageCount,
                CadStatus = details.CadStatus,
                HasNewerReleasedRevision = details.HasNewerReleasedRevision,
                ResolutionFailed = details.ResolutionFailed,
                ResolutionError = details.ResolutionError,
                CanAddToProject = details.CanAddToProject,
                WhereUsedSummary = L(TranslationKeys.LibraryWhereUsedHint)
            };

            if (details.ResolutionFailed && !string.IsNullOrWhiteSpace(details.ResolutionError))
            {
                StatusMessage = details.ResolutionError;
            }
            else if (details.HasNewerReleasedRevision)
            {
                StatusMessage = L(TranslationKeys.LibraryWarningNewerReleasedRevision);
            }

            _ = LoadDetailTabsAsync(entryId);
        }

        private void ClearDetailTabs()
        {
            SelectedCadDetails = new CadDetailsView();
            SelectedBomDetails = new BomDetailsView { Items = new ObservableCollection<BomLineItemView>() };
            SelectedRevisionDetails = new RevisionDetailsView { Items = new ObservableCollection<RevisionHistoryItemView>() };
            SelectedWhereUsedDetails = new WhereUsedDetailsView { Items = new ObservableCollection<WhereUsedItemView>() };
            _lastLoadedDetailEntryId = null;
            DetailStatusMessage = null;
            DetailErrorMessage = null;
            NotifyDetailStateChanged();
        }

        private async Task LoadDetailTabsAsync(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId) || string.Equals(entryId, _lastLoadedDetailEntryId, StringComparison.OrdinalIgnoreCase))
                return;

            _lastLoadedDetailEntryId = entryId;
            IsLoadingDetails = true;
            DetailStatusMessage = L(TranslationKeys.LibraryStatusLoadingDetails);
            DetailErrorMessage = null;

            try
            {
                var bundle = await ActiveClient.GetDetailBundleAsync(entryId, CancellationToken.None).ConfigureAwait(true);

                if (!string.Equals(entryId, _lastLoadedDetailEntryId, StringComparison.OrdinalIgnoreCase))
                    return;

                if (bundle?.Cad != null)
                {
                    SelectedCadDetails = new CadDetailsView
                    {
                        PrimaryCadId = bundle.Cad.PrimaryCadId,
                        PrimaryCadNumber = bundle.Cad.PrimaryCadNumber,
                        PrimaryCadName = bundle.Cad.PrimaryCadName,
                        PrimaryCadState = bundle.Cad.PrimaryCadState,
                        FileId = bundle.Cad.FileId,
                        FileName = bundle.Cad.FileName,
                        FileVersion = bundle.Cad.FileVersion,
                        LockedBy = bundle.Cad.LockedBy,
                        HasNative = bundle.Cad.HasNative,
                        CadStatus = bundle.Cad.CadStatus,
                        PartId = SelectedEntryDetails?.PartId
                    };
                }
                else
                {
                    SelectedCadDetails = new CadDetailsView();
                }

                if (bundle?.Bom?.BomItems != null)
                {
                    SelectedBomDetails = new BomDetailsView
                    {
                        EntryId = entryId,
                        Items = new ObservableCollection<BomLineItemView>(
                            bundle.Bom.BomItems.Select(i => new BomLineItemView
                            {
                                ComponentPartId = i.ComponentPartId,
                                ComponentPartNumber = i.ComponentPartNumber,
                                ComponentName = i.ComponentName,
                                ComponentRevision = i.ComponentRevision,
                                Quantity = i.Quantity,
                                Unit = i.Unit
                            }))
                    };
                }
                else
                {
                    SelectedBomDetails = new BomDetailsView { Items = new ObservableCollection<BomLineItemView>() };
                }

                if (bundle?.Revisions?.RevisionHistory != null)
                {
                    SelectedRevisionDetails = new RevisionDetailsView
                    {
                        EntryId = entryId,
                        CurrentPartId = bundle.Revisions.CurrentPartId,
                        CurrentRevision = bundle.Revisions.CurrentRevision,
                        CurrentLifecycleState = bundle.Revisions.CurrentLifecycleState,
                        CurrentGeneration = bundle.Revisions.CurrentGeneration,
                        Items = new ObservableCollection<RevisionHistoryItemView>(
                            bundle.Revisions.RevisionHistory.Select(r => new RevisionHistoryItemView
                            {
                                PartId = r.PartId,
                                Revision = r.Revision,
                                Generation = r.Generation,
                                LifecycleState = r.LifecycleState,
                                ModifiedOn = r.ModifiedOn,
                                IsCurrent = r.IsCurrent
                            }))
                    };
                }
                else
                {
                    SelectedRevisionDetails = new RevisionDetailsView { Items = new ObservableCollection<RevisionHistoryItemView>() };
                }

                if (bundle?.WhereUsed?.WhereUsedItems != null)
                {
                    SelectedWhereUsedDetails = new WhereUsedDetailsView
                    {
                        EntryId = entryId,
                        Items = new ObservableCollection<WhereUsedItemView>(
                            bundle.WhereUsed.WhereUsedItems.Select(w => new WhereUsedItemView
                            {
                                ParentPartId = w.ParentPartId,
                                ParentPartNumber = w.ParentPartNumber,
                                ParentPartName = w.ParentPartName,
                                ParentRevision = w.ParentRevision,
                                ParentState = w.ParentState,
                                Quantity = w.Quantity,
                                Source = w.Source.ToString()
                            }))
                    };
                }
                else
                {
                    SelectedWhereUsedDetails = new WhereUsedDetailsView { Items = new ObservableCollection<WhereUsedItemView>() };
                }

                DetailStatusMessage = null;
                DetailErrorMessage = null;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                SelectedCadDetails = new CadDetailsView();
                SelectedBomDetails = CreateEmptyBom();
                SelectedRevisionDetails = CreateEmptyRevisions();
                SelectedWhereUsedDetails = CreateEmptyWhereUsed();
                DetailStatusMessage = null;
                DetailErrorMessage = L(TranslationKeys.LibraryPermissionDenied);
            }
            catch (ArasOperationException ex)
            {
                SelectedCadDetails = new CadDetailsView();
                SelectedBomDetails = CreateEmptyBom();
                SelectedRevisionDetails = CreateEmptyRevisions();
                SelectedWhereUsedDetails = CreateEmptyWhereUsed();
                DetailStatusMessage = null;
                DetailErrorMessage = Lf(TranslationKeys.LibraryStatusDetailLoadFailed, ex.Message);
            }
            catch (OperationCanceledException)
            {
                _lastLoadedDetailEntryId = null;
            }
            catch (Exception ex)
            {
                SelectedCadDetails = new CadDetailsView();
                SelectedBomDetails = CreateEmptyBom();
                SelectedRevisionDetails = CreateEmptyRevisions();
                SelectedWhereUsedDetails = CreateEmptyWhereUsed();
                DetailStatusMessage = null;
                DetailErrorMessage = Lf(TranslationKeys.LibraryStatusDetailLoadFailed, ex.Message);
            }
            finally
            {
                IsLoadingDetails = false;
                NotifyDetailStateChanged();
            }
        }

        private async Task ViewWhereUsedAsync()
        {
            if (SelectedEntry == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            var partId = SelectedEntryDetails?.PartId ?? SelectedEntry.PartId;
            if (string.IsNullOrWhiteSpace(partId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusNoResolvedPartId);
                return;
            }

            await RunBusyAsync(async () =>
            {
                var whereUsed = await ActiveClient.GetWhereUsedAsync(partId, CancellationToken.None).ConfigureAwait(true);
                var summary = BuildWhereUsedSummary(whereUsed);
                SelectedEntryDetails = CloneDetailsWithWhereUsed(SelectedEntryDetails, summary);
                StatusMessage = whereUsed.Count == 0
                    ? L(TranslationKeys.LibraryStatusWhereUsedEmpty)
                    : Lf(TranslationKeys.LibraryStatusWhereUsedLoaded, whereUsed.Count);
            });
        }

        private async Task AddToCurrentProjectAsync()
        {
            if (_isAddingToCurrentProject)
                return;

            _isAddingToCurrentProject = true;
            RaiseCommandStates();

            try
            {
                var workspace = _session.CurrentPdmProjectsViewModel;
                if (workspace == null || SelectedEntry == null)
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusOpenPdmWorkspaceFirst);
                    return;
                }

                if (string.IsNullOrWhiteSpace(workspace.FolderPath))
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusOpenPdmWorkspaceFirst);
                    return;
                }

                if (SelectedEntry.IsDeprecated)
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusEntryDeprecated);
                    return;
                }

                if (SelectedEntry.ResolutionFailed || !SelectedEntry.CanAddToProject)
                {
                    StatusMessage = SelectedEntry.ResolutionError ?? SelectedEntryDetails?.ResolutionError ?? L(TranslationKeys.LibraryStatusNoRevisionResolution);
                    return;
                }

                var validParentCandidates = workspace.GetLibraryParentCandidates()
                    .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.LogicalCode))
                    .ToList() ?? new List<LibraryParentCandidate>();
                Debug.WriteLine(
                    "Part Library add: EntryId=" + SelectedEntry.EntryId +
                    ", ParentCandidateCount=" + validParentCandidates.Count + ".");

                if (validParentCandidates.Count == 0)
                {
                    StatusMessage = L(NoValidTargetParentMessage);
                    return;
                }

                // Resolve according to Entry revision policy first
                ResolveLibraryPartResult resolved = null;
                await RunBusyAsync(async () =>
                {
                    try
                    {
                        resolved = await ActiveClient.ResolveUsingStoredPolicyAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
                    }
                    catch (ArasOperationException ex)
                    {
                        StatusMessage = ex.Message;
                    }
                }).ConfigureAwait(true);

                if (resolved == null || string.IsNullOrWhiteSpace(resolved.ResolvedPartId))
                {
                    if (string.IsNullOrWhiteSpace(StatusMessage))
                        StatusMessage = L(TranslationKeys.LibraryStatusNoRevisionResolution);
                    return;
                }

                if (string.IsNullOrWhiteSpace(resolved.ResolvedPartConfigId) || string.IsNullOrWhiteSpace(resolved.ResolvedRevision))
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusNoRevisionResolution);
                    return;
                }

                var resolvedPartId = resolved.ResolvedPartId;
                var resolvedConfigId = resolved.ResolvedPartConfigId;
                var resolvedRevision = resolved.ResolvedRevision;
                var resolvedPartNumber = SelectedEntryDetails?.PartNumber ?? SelectedEntry.PartNumber;
                var resolvedPartName = SelectedEntryDetails?.PartName ?? SelectedEntry.PartName;
                Debug.WriteLine(
                    "Part Library add resolved: EntryId=" + SelectedEntry.EntryId +
                    ", PartId=" + resolvedPartId +
                    ", ConfigId=" + resolvedConfigId + ".");

                var dialogViewModel = new AddLibraryPartToProjectDialogViewModel(
                    workspace,
                    resolvedPartNumber,
                    resolvedPartName,
                    resolvedRevision,
                    resolvedPartId,
                    validParentCandidates);

                var accepted = AddToCurrentProjectDialogHandler?.Invoke(dialogViewModel);
                Debug.WriteLine(
                    "Part Library add dialog: EntryId=" + SelectedEntry.EntryId +
                    ", Accepted=" + (accepted == true) + ".");
                if (accepted != true)
                    return;

                if (dialogViewModel.SelectedParent == null || string.IsNullOrWhiteSpace(dialogViewModel.SelectedParent.LogicalCode))
                {
                    StatusMessage = L(NoValidTargetParentMessage);
                    return;
                }

                if (dialogViewModel.ParsedQuantity <= 0)
                {
                    StatusMessage = L(TranslationKeys.LibraryAddDialogValidationMessage);
                    return;
                }
                Debug.WriteLine(
                    "Part Library add placement: EntryId=" + SelectedEntry.EntryId +
                    ", ParentLogicalCode=" + dialogViewModel.SelectedParent.LogicalCode +
                    ", Quantity=" + dialogViewModel.ParsedQuantity + ".");

                var reference = new WorkspaceLibraryReference
                {
                    ReferenceId = Guid.NewGuid().ToString("N"),
                    LibraryId = SelectedEntry.LibraryId,
                    LibraryEntryId = SelectedEntry.EntryId,
                    PartId = resolvedPartId,
                    PartConfigId = resolvedConfigId,
                    PartNumber = resolvedPartNumber,
                    PartName = resolvedPartName,
                    Revision = resolvedRevision,
                    ParentLogicalCode = dialogViewModel.SelectedParent.LogicalCode,
                    LocalLogicalCode = "LIB-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                    Quantity = dialogViewModel.ParsedQuantity,
                    RevisionPolicy = SelectedEntry.RevisionPolicy,
                    AddedOn = DateTime.UtcNow,
                    AddedBy = _session.CurrentUserName ?? L(TranslationKeys.DisplayEngineer)
                };

                var result = AddLibraryReferenceHandler(workspace, reference);
                Debug.WriteLine(
                    "Part Library add result: EntryId=" + SelectedEntry.EntryId +
                    ", Success=" + result.Success + ".");
                StatusMessage = result.Message;

                if (result.Success)
                    await SearchAsync().ConfigureAwait(true);
            }
            catch (ArasOperationException ex)
            {
                Debug.WriteLine(ex.ToString());
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                StatusMessage = Lf(TranslationKeys.LibraryFailedPrefix, "Failed to add Library Part to the current PDM Project: " + ex.Message);
            }
            finally
            {
                _isAddingToCurrentProject = false;
                RaiseCommandStates();
            }
        }

        private bool CanAddToCurrentProject()
        {
            return SelectedEntry != null &&
                   HasActivePdmWorkspace &&
                   !IsLoading &&
                   !_isAddingToCurrentProject &&
                   !SelectedEntry.IsDeprecated &&
                   SelectedEntry.CanAddToProject &&
                   !SelectedEntry.ResolutionFailed;
        }

        private bool? ShowAddToCurrentProjectDialog(AddLibraryPartToProjectDialogViewModel dialogViewModel)
        {
            if (dialogViewModel == null)
                throw new ArgumentNullException(nameof(dialogViewModel));

            var dialog = new AddLibraryPartToProjectDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = dialogViewModel
            };

            dialogViewModel.CloseRequested += accepted => dialog.DialogResult = accepted;

            return dialog.ShowDialog();
        }

        private void ShowSaveToLibraryDialog()
        {
            if (!CanContributeToSelectedLibrary)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                return;
            }

            var dialog = new SaveToLibraryDialog
            {
                Owner = Application.Current?.MainWindow
            };

            dialog.ShowDialog();
            StatusMessage = L(TranslationKeys.LibraryStatusSaveDialogOpened);
        }

        private async Task ShowCreateLibraryDialogAsync()
        {
            if (!CanManageLibraries)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusCreateLibraryUnavailable);
                return;
            }

            var viewModel = new CreateLibraryDialogViewModel(ActiveClient);
            await viewModel.InitializeAsync().ConfigureAwait(true);

            if (CreateLibraryDialogHandler?.Invoke(viewModel) == true)
            {
                if (!string.IsNullOrWhiteSpace(viewModel.CreatedLibraryId))
                    _session.PendingLibraryFocusLibraryId = viewModel.CreatedLibraryId;

                await RefreshAsync().ConfigureAwait(true);
            }
        }

        private async Task ShowEditLibraryDialogAsync()
        {
            if (SelectedLibrary == null || !CanEditSelectedLibrary)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                return;
            }

            var library = new PartLibrarySummary
            {
                Id = SelectedLibrary.Id,
                Name = SelectedLibrary.Name,
                LibraryType = (LibraryType)Enum.Parse(typeof(LibraryType), SelectedLibrary.LibraryType),
                Description = SelectedLibrary.Description,
                IsPublic = SelectedLibrary.IsPublic,
                CanContribute = SelectedLibrary.CanContribute,
                ItemCount = SelectedLibrary.ItemCount,
                Status = SelectedLibrary.Status,
                DefaultRevisionPolicy = SelectedLibrary.DefaultRevisionPolicy
            };

            var viewModel = new EditLibraryDialogViewModel(ActiveClient, library);
            await viewModel.InitializeAsync().ConfigureAwait(true);

            if (EditLibraryDialogHandler?.Invoke(viewModel) == true)
            {
                _session.PendingLibraryFocusLibraryId = SelectedLibrary.Id;
                await RefreshAsync().ConfigureAwait(true);
            }
        }

        private async Task ShowArchiveLibraryFlowAsync()
        {
            if (SelectedLibrary == null || !CanArchiveSelectedLibrary)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                return;
            }

            var confirm = ConfirmDialogHandler?.Invoke(
                L(TranslationKeys.ArchiveLibraryConfirmation),
                L(TranslationKeys.ArchiveLibraryTitle),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) ?? MessageBoxResult.No;

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunBusyAsync(async () =>
            {
                var result = await ActiveClient.ArchiveLibraryAsync(SelectedLibrary.Id, CancellationToken.None).ConfigureAwait(true);

                if (result?.Success == true)
                {
                    StatusMessage = L(TranslationKeys.ArchiveLibrarySuccess);
                    await RefreshAsync().ConfigureAwait(true);
                }
                else if (result?.ErrorCode == ArasErrorCode.PermissionDenied)
                {
                    StatusMessage = L(TranslationKeys.ArchiveLibraryPermissionDenied);
                }
                else
                {
                    StatusMessage = result?.ErrorMessage ?? L(TranslationKeys.UnknownError);
                }
            });
        }

        private async Task ShowArasPartPickerDialogAsync()
        {
            if (!CanContributeToSelectedLibrary)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                return;
            }

            var viewModel = new ArasPartPickerViewModel(ActiveClient);
            await viewModel.InitializeAsync().ConfigureAwait(true);

            if (PartPickerDialogHandler?.Invoke(viewModel) == true)
            {
                if (!string.IsNullOrWhiteSpace(viewModel.TargetLibrary?.Id))
                    _session.PendingLibraryFocusLibraryId = viewModel.TargetLibrary.Id;
                if (!string.IsNullOrWhiteSpace(viewModel.AddResult?.EntryId))
                    _session.PendingLibraryFocusEntryId = viewModel.AddResult.EntryId;

                StatusMessage = L(TranslationKeys.PartPickerAddSuccess);
                await RefreshAsync().ConfigureAwait(true);
            }
        }

        private async Task RemoveSelectedEntryAsync()
        {
            if (SelectedEntry == null)
                return;

            var confirm = MessageBox.Show(
                L(TranslationKeys.LibraryConfirmRemoveEntry),
                L(TranslationKeys.LibraryDialogActionTitle),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunBusyAsync(async () =>
            {
                await ActiveClient.RemoveEntryAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
                StatusMessage = L(TranslationKeys.LibraryStatusEntryRemoved);
                await RefreshAsync().ConfigureAwait(true);
            });
        }

        private bool CanExecuteMoveEntry()
        {
            if (SelectedEntry == null || SelectedLibrary == null || IsLoading || IsOffline)
                return false;

            if (!(_authService?.CanMoveEntries ?? false))
                return false;

            if (SelectedLibrary.IsArchived)
                return false;

            return true;
        }

        private bool CanExecuteShowRevisionBrowser()
        {
            if (SelectedEntry == null || IsLoading || IsOffline)
                return false;

            if (IsReadOnlyViewer)
                return false;

            return true;
        }

        private async Task MoveSelectedEntryAsync()
        {
            if (!CanExecuteMoveEntry())
                return;

            if (string.IsNullOrWhiteSpace(SelectedEntry.EntryId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            var entrySummary = new PartLibraryEntrySummary
            {
                EntryId = SelectedEntry.EntryId,
                LibraryId = SelectedEntry.LibraryId,
                LibraryName = SelectedEntry.LibraryName,
                PartId = SelectedEntry.PartId,
                PartConfigId = SelectedEntry.PartConfigId,
                PartNumber = SelectedEntry.PartNumber,
                PartName = SelectedEntry.PartName,
                PartType = SelectedEntry.PartType,
                Revision = SelectedEntry.Revision,
                LifecycleState = SelectedEntry.LifecycleState,
                EntryLifecycleState = SelectedEntry.EntryLifecycleState,
                RevisionPolicy = ParseRevisionPolicy(SelectedEntry.RevisionPolicy),
                EntryStatus = ParseEntryStatus(SelectedEntry.EntryStatus),
                CadStatus = SelectedEntry.CadStatus,
                UsageCount = SelectedEntry.UsageCount,
                HasNewerReleasedRevision = SelectedEntry.HasNewerReleasedRevision,
                IsDeprecated = SelectedEntry.IsDeprecated,
                ResolutionFailed = SelectedEntry.ResolutionFailed,
                ResolutionError = SelectedEntry.ResolutionError,
                CanAddToProject = SelectedEntry.CanAddToProject
            };

            var viewModel = new MoveLibraryEntryDialogViewModel(ActiveClient, entrySummary, SelectedLibrary.Id);
            await viewModel.InitializeAsync().ConfigureAwait(true);

            if (MoveEntryDialogHandler?.Invoke(viewModel) == true)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusEntryMovedSuccess);
                await RefreshAsync().ConfigureAwait(true);

                if (!string.IsNullOrWhiteSpace(viewModel.MoveResult?.EntryId))
                    _session.PendingLibraryFocusEntryId = viewModel.MoveResult.EntryId;
            }
            else
            {
                if (viewModel.MoveResult?.Success == false)
                {
                    StatusMessage = viewModel.MoveResult.ErrorMessage ?? L(TranslationKeys.LibraryStatusMoveEntryFailed);
                }
            }
        }

        private async Task ShowRevisionBrowserDialogAsync()
        {
            if (!CanExecuteShowRevisionBrowser())
                return;

            if (SelectedEntry == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            var viewModel = new PartRevisionBrowserViewModel(
                ActiveClient,
                SelectedEntry,
                SelectedEntryDetails?.RevisionPolicy ?? SelectedEntry.RevisionPolicy,
                _authService?.CanPinRevisions ?? false);

            await viewModel.InitializeAsync().ConfigureAwait(true);

            if (RevisionBrowserDialogHandler?.Invoke(viewModel) == true)
            {
            }

            if (viewModel.PinSuccess)
            {
                StatusMessage = L(TranslationKeys.RevisionBrowserPinSuccess);
                await RefreshAsync().ConfigureAwait(true);
            }
        }

        private async Task DeprecateSelectedEntryAsync()
        {
            if (SelectedEntry == null)
                return;

            var confirm = MessageBox.Show(
                L(TranslationKeys.LibraryConfirmDeprecateEntry),
                L(TranslationKeys.LibraryDialogActionTitle),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunBusyAsync(async () =>
            {
                await ActiveClient.DeprecateEntryAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
                await RefreshAsync().ConfigureAwait(true);
                StatusMessage = L(TranslationKeys.LibraryStatusEntryDeprecated);
            });
        }

        private async Task PublishSelectedEntryAsync()
        {
            if (SelectedEntry == null)
                return;

            var confirm = MessageBox.Show(
                L(TranslationKeys.LibraryConfirmPublishEntry),
                L(TranslationKeys.LibraryDialogActionTitle),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunBusyAsync(async () =>
            {
                await ActiveClient.PublishEntryAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
                await RefreshAsync().ConfigureAwait(true);
                StatusMessage = L(TranslationKeys.LibraryStatusEntryPublished);
            });
        }

        private async Task ResolveSelectedEntryAsync(LibraryRevisionPolicy policy)
        {
            if (SelectedEntry == null)
                return;

            await RunBusyAsync(async () =>
            {
                var request = new UpdateLibraryRevisionPolicyRequest
                {
                    EntryId = SelectedEntry.EntryId,
                    RevisionPolicy = policy,
                    PinnedPartId = policy == LibraryRevisionPolicy.Pinned
                        ? SelectedEntry.PartId
                        : null
                };

                var result = await ActiveClient.UpdateRevisionPolicyAsync(request, CancellationToken.None).ConfigureAwait(true);

                if (!result.Success)
                {
                    StatusMessage = result.ErrorMessage ?? L(TranslationKeys.LibraryStatusNoRevisionResolution);
                    return;
                }

                SelectedEntryDetails = new PartLibraryEntryDetailsView
                {
                    EntryId = result.EntryId,
                    PartId = result.ResolvedPartId,
                    PartConfigId = result.ResolvedPartConfigId,
                    Revision = result.ResolvedRevision,
                    RevisionPolicy = result.RevisionPolicy.ToString(),
                    PartNumber = SelectedEntryDetails?.PartNumber ?? SelectedEntry?.PartNumber,
                    PartName = SelectedEntryDetails?.PartName ?? SelectedEntry?.PartName,
                    CanAddToProject = true,
                    ResolutionFailed = false,
                    ResolutionError = null,
                    WhereUsedSummary = SelectedEntryDetails?.WhereUsedSummary ?? L(TranslationKeys.LibraryWhereUsedHint)
                };

                StatusMessage = policy == LibraryRevisionPolicy.Pinned
                    ? L(TranslationKeys.LibraryStatusPinnedLoaded)
                    : L(TranslationKeys.LibraryStatusLatestReleasedLoaded);

                await RefreshAsync().ConfigureAwait(true);
            });
        }

        private async Task DownloadCadAsync()
        {
            if (_vaultService == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusVaultServiceUnavailable);
                return;
            }

            var entryId = SelectedEntry?.EntryId ?? SelectedEntryDetails?.EntryId;
            if (string.IsNullOrWhiteSpace(entryId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            PartLibraryCadFileInfo cadInfo = null;
            try
            {
                cadInfo = await _vaultService.GetPrimaryCadFileInfoAsync(entryId, CancellationToken.None).ConfigureAwait(true);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                StatusMessage = L(TranslationKeys.LibraryCadNoCadFound);
                return;
            }
            catch (ArasOperationException ex)
            {
                StatusMessage = ex.Message;
                return;
            }

            if (cadInfo == null || !cadInfo.HasNative)
            {
                StatusMessage = L(TranslationKeys.LibraryCadNoCadFound);
                return;
            }

            await RunBusyAsync(async () =>
            {
                var result = await _vaultService.DownloadToCacheAsync(cadInfo, CancellationToken.None).ConfigureAwait(true);

                if (result.Success)
                {
                    StatusMessage = Lf(TranslationKeys.LibraryStatusDownloadSucceeded, result.LocalFilePath);
                }
                else
                {
                    StatusMessage = result.ErrorMessage ?? L(TranslationKeys.LibraryStatusDownloadFailed);
                }
            });
        }

        private async Task OpenPrimaryCadAsync()
        {
            if (_vaultService == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusVaultServiceUnavailable);
                return;
            }

            var entryId = SelectedEntry?.EntryId ?? SelectedEntryDetails?.EntryId;
            if (string.IsNullOrWhiteSpace(entryId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            PartLibraryCadFileInfo cadInfo = null;
            try
            {
                cadInfo = await _vaultService.GetPrimaryCadFileInfoAsync(entryId, CancellationToken.None).ConfigureAwait(true);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.CadNotFound)
            {
                StatusMessage = L(TranslationKeys.LibraryCadNoCadFound);
                return;
            }
            catch (ArasOperationException ex)
            {
                StatusMessage = ex.Message;
                return;
            }

            if (cadInfo == null || !cadInfo.HasNative)
            {
                StatusMessage = L(TranslationKeys.LibraryCadNoCadFound);
                return;
            }

            if (_ironCadService == null || !_ironCadService.IsIronCadAvailable)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusIronCadServiceUnavailable);
                return;
            }

            await RunBusyAsync(async () =>
            {
                // Try cache first
                var cached = _vaultService.GetCachedFilePath(cadInfo);
                if (string.IsNullOrWhiteSpace(cached))
                {
                    var download = await _vaultService.DownloadToCacheAsync(cadInfo, CancellationToken.None).ConfigureAwait(true);
                    if (!download.Success)
                    {
                        StatusMessage = download.ErrorMessage ?? L(TranslationKeys.LibraryStatusOpenInIronCadFailed);
                        return;
                    }
                    cached = download.LocalFilePath;
                }

                if (string.IsNullOrWhiteSpace(cached) || !File.Exists(cached))
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadNotFound);
                    return;
                }

                var fileInfo = new FileInfo(cached);
                var request = new IronCadOpenRequest
                {
                    FilePath = cached,
                    FileSize = fileInfo.Length,
                    IsRemoteUrl = false,
                    IsTrustedSource = true,
                    Source = "VaultCache"
                };

                var result = await _ironCadService.OpenCadFileAsync(request, CancellationToken.None).ConfigureAwait(true);

                if (result.Success)
                {
                    StatusMessage = L(TranslationKeys.LibraryStatusOpenInIronCadSucceeded);
                }
                else
                {
                    StatusMessage = result.ErrorMessage ?? L(TranslationKeys.LibraryStatusOpenInIronCadFailed);
                }
            });
        }

        private async Task OpenSelectedPartInArasAsync()
        {
            var partId = SelectedEntryDetails?.PartId ?? SelectedEntry?.PartId;
            if (string.IsNullOrWhiteSpace(partId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusNoResolvedPartId);
                return;
            }

            await OpenInArasAsync(new ArasOpenUrlRequest
            {
                ItemType = "Part",
                ItemId = partId,
                ConfigId = SelectedEntryDetails?.PartConfigId
            });
        }

        private async Task OpenSelectedEntryInArasAsync()
        {
            var entryId = SelectedEntryDetails?.EntryId ?? SelectedEntry?.EntryId;
            if (string.IsNullOrWhiteSpace(entryId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectPartFirst);
                return;
            }

            await OpenInArasAsync(new ArasOpenUrlRequest
            {
                ItemType = "idea_PartLibraryEntry",
                ItemId = entryId
            });
        }

        private async Task OpenSelectedLibraryInArasAsync()
        {
            var libraryId = SelectedLibrary?.Id;
            if (string.IsNullOrWhiteSpace(libraryId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectDifferentLibrary);
                return;
            }

            await OpenInArasAsync(new ArasOpenUrlRequest
            {
                ItemType = "idea_PartLibrary",
                ItemId = libraryId
            });
        }

        private async Task OpenSelectedCadInArasAsync()
        {
            var cadId = SelectedCadDetails?.PrimaryCadId ?? SelectedEntryDetails?.PrimaryCadId;
            if (string.IsNullOrWhiteSpace(cadId))
            {
                StatusMessage = L(TranslationKeys.LibraryCadNoCadFound);
                return;
            }

            await OpenInArasAsync(new ArasOpenUrlRequest
            {
                ItemType = "CAD",
                ItemId = cadId
            });
        }

        private async Task OpenInArasAsync(ArasOpenUrlRequest request)
        {
            if (_openUrlService == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusBrowserLauncherUnavailable);
                return;
            }

            ArasOpenUrlResult urlResult;
            try
            {
                urlResult = await _openUrlService.BuildUrlAsync(request, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusMessage = Lf(TranslationKeys.LibraryStatusOpenInArasFailed, ex.Message);
                return;
            }

            if (!urlResult.Success || string.IsNullOrWhiteSpace(urlResult.Url))
            {
                StatusMessage = urlResult.ErrorMessage ?? L(TranslationKeys.LibraryStatusOpenInArasFailed);
                return;
            }

            if (_browserLauncher == null)
            {
                StatusMessage = L(TranslationKeys.LibraryStatusBrowserLauncherUnavailable);
                return;
            }

            var launched = await _browserLauncher.LaunchUrlAsync(urlResult.Url, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = launched
                ? L(TranslationKeys.LibraryStatusOpenInArasSucceeded)
                : Lf(TranslationKeys.LibraryStatusOpenInArasFailed, urlResult.Url);
        }

        private string FindWorkspaceCadFile(string fileName)
        {
            var workspaceFolder = _session.CurrentPdmProjectsViewModel?.FolderPath;
            if (string.IsNullOrWhiteSpace(workspaceFolder) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(workspaceFolder))
                return null;

            try
            {
                return Directory.GetFiles(workspaceFolder, fileName, SearchOption.AllDirectories).FirstOrDefault();
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private async Task RunBusyAsync(Func<Task> action)
        {
            try
            {
                IsLoading = true;
                await action().ConfigureAwait(true);
                OnPropertyChanged(nameof(IsOffline));
                OnPropertyChanged(nameof(HasActivePdmWorkspace));
                OnPropertyChanged(nameof(ConnectionTitle));
                OnPropertyChanged(nameof(ConnectionDatabase));
            }
            catch (ArasOperationException ex)
            {
                if (ex.ErrorCode == ArasErrorCode.PermissionDenied)
                {
                    _permissionMessage = L(TranslationKeys.LibraryPermissionDenied);
                    _errorMessage = string.Empty;
                }
                else
                {
                    _permissionMessage = string.Empty;
                    _errorMessage = ex.Message;
                }

                NotifyPanelStateChanged();
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                _permissionMessage = string.Empty;
                _errorMessage = ex.Message;
                NotifyPanelStateChanged();
                StatusMessage = Lf(TranslationKeys.LibraryFailedPrefix, ex.Message);
            }
            finally
            {
                IsLoading = false;
                NotifyPanelStateChanged();
            }
        }

        private string NormalizeTypeFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllTypes))
                return null;
            if (value == L(TranslationKeys.LibraryFilterAssembly))
                return L(TranslationKeys.DisplayAssembly);
            if (value == L(TranslationKeys.LibraryFilterComponent))
                return L(TranslationKeys.DisplayComponent);
            return value;
        }

        private string NormalizeStateFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllStates))
                return null;
            if (value == L(TranslationKeys.LibraryFilterReleased))
                return L(TranslationKeys.DisplayReleased);
            if (value == L(TranslationKeys.LibraryFilterInReview))
                return L(TranslationKeys.DisplayInReview);
            if (value == L(TranslationKeys.LibraryFilterPreliminary))
                return L(TranslationKeys.DisplayPreliminary);
            return value;
        }

        private string NormalizeRevisionFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllRevisions))
                return null;
            if (value == L(TranslationKeys.LibraryFilterLatestReleased))
                return "LatestReleased";
            if (value == L(TranslationKeys.LibraryFilterPinned))
                return "Pinned";
            if (value == L(TranslationKeys.LibraryFilterLatestCurrent))
                return "LatestCurrent";
            return value;
        }

        private string NormalizeEntryStatusFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllEntryStatuses))
                return null;
            if (value == L(TranslationKeys.LibraryFilterEntryStatusDraft))
                return L(TranslationKeys.DisplayDraft);
            if (value == L(TranslationKeys.LibraryFilterEntryStatusPendingReview))
                return L(TranslationKeys.DisplayPendingReview);
            if (value == L(TranslationKeys.LibraryFilterEntryStatusPublished))
                return L(TranslationKeys.DisplayPublished);
            if (value == L(TranslationKeys.LibraryFilterEntryStatusDeprecated))
                return L(TranslationKeys.DisplayDeprecated);
            return value;
        }

        private string NormalizeCadStatusFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllCadStatuses))
                return null;
            if (value == L(TranslationKeys.LibraryFilterCadStatusAvailable) || value == L(TranslationKeys.LibraryCadStatusAvailable))
                return L(TranslationKeys.DisplayAvailable);
            if (value == L(TranslationKeys.LibraryFilterCadStatusNoCad) || value == L(TranslationKeys.LibraryCadStatusNoCad))
                return L(TranslationKeys.DisplayNoCad);
            if (value == L(TranslationKeys.LibraryFilterCadStatusNoNativeFile) || value == L(TranslationKeys.LibraryCadStatusNoNative))
                return L(TranslationKeys.DisplayNoNativeFile);
            if (value == L(TranslationKeys.LibraryFilterCadStatusLookupUnavailable) || value == L(TranslationKeys.LibraryCadLookupUnavailable))
                return L(TranslationKeys.DisplayCadLookupUnavailable);
            return value;
        }

        private static IEnumerable<PartLibraryEntryRow> SortEntries(ObservableCollection<PartLibraryEntryRow> entries, string sortOption, string sortDirection)
        {
            if (entries == null || entries.Count == 0)
                return Enumerable.Empty<PartLibraryEntryRow>();

            bool descending = sortDirection != null && sortDirection.Contains(L(TranslationKeys.DisplayDescending));

            if (sortOption == null || sortOption.Contains(L(TranslationKeys.DisplayItemNumber)))
                return descending
                    ? entries.OrderByDescending(e => e.PartNumber, StringComparer.OrdinalIgnoreCase)
                    : entries.OrderBy(e => e.PartNumber, StringComparer.OrdinalIgnoreCase);
            if (sortOption.Contains(L(TranslationKeys.LibrarySortByName)))
                return descending
                    ? entries.OrderByDescending(e => e.PartName, StringComparer.OrdinalIgnoreCase)
                    : entries.OrderBy(e => e.PartName, StringComparer.OrdinalIgnoreCase);
            if (sortOption.Contains(L(TranslationKeys.DisplayEntryStatus)))
                return descending
                    ? entries.OrderByDescending(e => e.EntryStatus)
                    : entries.OrderBy(e => e.EntryStatus);
            if (sortOption.Contains(L(TranslationKeys.DisplayRevisionPolicy)))
                return descending
                    ? entries.OrderByDescending(e => e.RevisionPolicy)
                    : entries.OrderBy(e => e.RevisionPolicy);
            if (sortOption.Contains(L(TranslationKeys.DisplayCadStatus)))
                return descending
                    ? entries.OrderByDescending(e => e.CadStatus)
                    : entries.OrderBy(e => e.CadStatus);
            if (sortOption.Contains(L(TranslationKeys.DisplayUsageCount)))
                return descending
                    ? entries.OrderByDescending(e => e.UsageCount)
                    : entries.OrderBy(e => e.UsageCount);
            if (sortOption.Contains(L(TranslationKeys.DisplayLastUsed)))
                return entries;
            return entries.OrderBy(e => e.PartNumber, StringComparer.OrdinalIgnoreCase);
        }

        private static PartLibraryEntryRow MapEntry(PartLibraryEntrySummary entry)
        {
            return new PartLibraryEntryRow
            {
                EntryId = entry.EntryId,
                LibraryId = entry.LibraryId,
                PartId = entry.PartId,
                PartConfigId = entry.PartConfigId,
                PartNumber = entry.PartNumber,
                PartName = entry.PartName,
                PartType = entry.PartType,
                Revision = entry.Revision,
                LifecycleState = entry.LifecycleState,
                EntryLifecycleState = entry.EntryLifecycleState,
                EntryStatus = entry.EntryStatus.ToString(),
                RevisionPolicy = entry.RevisionPolicy.ToString(),
                CadStatus = entry.CadStatus,
                UsageCount = entry.UsageCount,
                HasNewerReleasedRevision = entry.HasNewerReleasedRevision,
                IsDeprecated = entry.IsDeprecated,
                ResolutionFailed = entry.ResolutionFailed,
                ResolutionError = entry.ResolutionError,
                CanAddToProject = entry.CanAddToProject,
                LibraryName = entry.LibraryName
            };
        }

        private string BuildWhereUsedSummary(IReadOnlyList<PartWhereUsedItem> whereUsed)
        {
            if (whereUsed == null || whereUsed.Count == 0)
                return L(TranslationKeys.LibraryWhereUsedNone);

            var lines = whereUsed
                .Take(6)
                .Select(item =>
                {
                    var parent = item.ParentPartNumber ?? item.ParentPartName ?? L(TranslationKeys.LibraryWhereUsedUnknownParent);
                    var name = string.IsNullOrWhiteSpace(item.ParentPartName) ? string.Empty : " - " + item.ParentPartName;
                    var quantity = item.Quantity > 0 ? " (qty " + item.Quantity + ")" : string.Empty;
                    return parent + name + quantity;
                })
                .ToList();

            if (whereUsed.Count > lines.Count)
                lines.Add(Lf(TranslationKeys.LibraryWhereUsedMore, whereUsed.Count - lines.Count));

            return string.Join(Environment.NewLine, lines);
        }

        private PartLibraryEntryDetailsView CreateEmptyDetails()
        {
            return new PartLibraryEntryDetailsView
            {
                PartNumber = L(TranslationKeys.LibrarySelectPart),
                PrimaryCadFileName = L(TranslationKeys.LibraryNoLinkedCad),
                HasNewerReleasedRevision = false,
                CanAddToProject = false,
                WhereUsedSummary = L(TranslationKeys.LibraryWhereUsedHint)
            };
        }

        private BomDetailsView CreateEmptyBom()
        {
            return new BomDetailsView { Items = new ObservableCollection<BomLineItemView>() };
        }

        private RevisionDetailsView CreateEmptyRevisions()
        {
            return new RevisionDetailsView { Items = new ObservableCollection<RevisionHistoryItemView>() };
        }

        private WhereUsedDetailsView CreateEmptyWhereUsed()
        {
            return new WhereUsedDetailsView { Items = new ObservableCollection<WhereUsedItemView>() };
        }

        private static PartLibraryEntryDetailsView CloneDetailsWithWhereUsed(PartLibraryEntryDetailsView details, string whereUsedSummary)
        {
            details = details ?? new PartLibraryEntryDetailsView();
            return new PartLibraryEntryDetailsView
            {
                EntryId = details.EntryId,
                LibraryId = details.LibraryId,
                LibraryName = details.LibraryName,
                PartId = details.PartId,
                PartConfigId = details.PartConfigId,
                PartNumber = details.PartNumber,
                PartName = details.PartName,
                PartType = details.PartType,
                Revision = details.Revision,
                Generation = details.Generation,
                LifecycleState = details.LifecycleState,
                EntryLifecycleState = details.EntryLifecycleState,
                RevisionPolicy = details.RevisionPolicy,
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
                PrimaryCadFileId = details.PrimaryCadFileId,
                LockedBy = details.LockedBy,
                UsageCount = details.UsageCount,
                CadStatus = details.CadStatus,
                HasNewerReleasedRevision = details.HasNewerReleasedRevision,
                ResolutionFailed = details.ResolutionFailed,
                ResolutionError = details.ResolutionError,
                CanAddToProject = details.CanAddToProject,
                WhereUsedSummary = whereUsedSummary
            };
        }

        private static PartLibraryEntryDetailsView CloneDetailsWithResolution(
            PartLibraryEntryDetailsView details,
            ResolveLibraryPartResult resolved,
            LibraryRevisionPolicy policy)
        {
            details = details ?? new PartLibraryEntryDetailsView();
            return new PartLibraryEntryDetailsView
            {
                EntryId = details.EntryId,
                LibraryId = details.LibraryId,
                LibraryName = details.LibraryName,
                PartId = resolved?.ResolvedPartId ?? details.PartId,
                PartConfigId = resolved?.ResolvedPartConfigId ?? details.PartConfigId,
                PartNumber = details.PartNumber,
                PartName = details.PartName,
                PartType = details.PartType,
                Revision = resolved?.ResolvedRevision ?? details.Revision,
                LifecycleState = resolved?.LifecycleState ?? details.LifecycleState,
                RevisionPolicy = policy.ToString(),
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = resolved?.CadStatus ?? details.PrimaryCadState,
                LockedBy = details.LockedBy,
                UsageCount = details.UsageCount,
                CadStatus = resolved?.CadStatus ?? details.CadStatus,
                HasNewerReleasedRevision = details.HasNewerReleasedRevision,
                WhereUsedSummary = details.WhereUsedSummary
            };
        }

        private void RaiseCommandStates()
        {
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CreateLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ArchiveLibraryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ShowPartPickerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddPartCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ShowRevisionBrowserCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddToCurrentProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenInIronCadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PublishCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeprecateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PinRevisionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (UseLatestReleasedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ViewWhereUsedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSelectedPartInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSelectedEntryInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSelectedLibraryInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSelectedCadInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ClearTransientStates()
        {
            _permissionMessage = string.Empty;
            _errorMessage = string.Empty;
            NotifyPanelStateChanged();
        }

        private void NotifyPanelStateChanged()
        {
            OnPropertyChanged(nameof(CanContributeToSelectedLibrary));
            OnPropertyChanged(nameof(CanCreateLibrary));
            OnPropertyChanged(nameof(CanUsePartPicker));
            OnPropertyChanged(nameof(CanAddEntryToSelectedLibrary));
            OnPropertyChanged(nameof(CanUsePartPickerForSelectedLibrary));
            OnPropertyChanged(nameof(IsLibraryManager));
            OnPropertyChanged(nameof(IsContributorOrHigher));
            OnPropertyChanged(nameof(IsReadOnlyViewer));
            OnPropertyChanged(nameof(CanManageLibraries));
            OnPropertyChanged(nameof(CanEditSelectedLibrary));
            OnPropertyChanged(nameof(CanArchiveSelectedLibrary));
            OnPropertyChanged(nameof(IsPermissionState));
            OnPropertyChanged(nameof(IsErrorState));
            OnPropertyChanged(nameof(ShowLibrariesOverlay));
            OnPropertyChanged(nameof(ShowEntriesOverlay));
            OnPropertyChanged(nameof(LibrariesOverlayMessage));
            OnPropertyChanged(nameof(EntriesOverlayMessage));
            OnPropertyChanged(nameof(AddToProjectHint));
            NotifyDetailStateChanged();
        }

        private void NotifyDetailStateChanged()
        {
            OnPropertyChanged(nameof(HasCadDetails));
            OnPropertyChanged(nameof(HasNoCadDetails));
            OnPropertyChanged(nameof(HasBomItems));
            OnPropertyChanged(nameof(HasNoBomItems));
            OnPropertyChanged(nameof(HasRevisionItems));
            OnPropertyChanged(nameof(HasNoRevisionItems));
            OnPropertyChanged(nameof(HasWhereUsedItems));
            OnPropertyChanged(nameof(HasNoWhereUsedItems));
        }

        private void OnLibraryDataChanged(object sender, EventArgs e)
        {
            RefreshLibraryServices();
            RaiseCommandStates();
            _ = RefreshAsync();
        }

        private void TryFocusPendingEntry()
        {
            var pendingEntryId = _session.PendingLibraryFocusEntryId;
            if (string.IsNullOrWhiteSpace(pendingEntryId))
                return;

            var matchedEntry = Entries.FirstOrDefault(item =>
                string.Equals(item.EntryId, pendingEntryId, StringComparison.OrdinalIgnoreCase));
            if (matchedEntry == null)
                return;

            SelectedEntry = matchedEntry;
            _session.PendingLibraryFocusEntryId = null;
            _session.PendingLibraryFocusLibraryId = null;
        }

        private void RefreshVisibilityFilterOptions()
        {
            var previousSelection = _selectedVisibilityFilter;

            VisibilityFilters.Clear();
            VisibilityFilters.Add(L(TranslationKeys.LibraryFilterActive));
            VisibilityFilters.Add(L(TranslationKeys.LibraryFilterArchived));
            VisibilityFilters.Add(L(TranslationKeys.LibraryFilterAll));

            if (!string.IsNullOrWhiteSpace(previousSelection) &&
                VisibilityFilters.Contains(previousSelection))
            {
                _selectedVisibilityFilter = previousSelection;
            }
            else
            {
                _selectedVisibilityFilter = VisibilityFilters[0];
            }

            OnPropertyChanged(nameof(SelectedVisibilityFilter));
        }

        private void RefreshEntryStatusFilterOptions()
        {
            var previousSelection = _selectedEntryStatusFilter;
            EntryStatusFilters.Clear();
            EntryStatusFilters.Add(L(TranslationKeys.LibraryFilterAllEntryStatuses));
            EntryStatusFilters.Add(L(TranslationKeys.LibraryFilterEntryStatusDraft));
            EntryStatusFilters.Add(L(TranslationKeys.LibraryFilterEntryStatusPendingReview));
            EntryStatusFilters.Add(L(TranslationKeys.LibraryFilterEntryStatusPublished));
            EntryStatusFilters.Add(L(TranslationKeys.LibraryFilterEntryStatusDeprecated));
            if (!string.IsNullOrWhiteSpace(previousSelection) && EntryStatusFilters.Contains(previousSelection))
                _selectedEntryStatusFilter = previousSelection;
            else
                _selectedEntryStatusFilter = EntryStatusFilters[0];
            OnPropertyChanged(nameof(SelectedEntryStatusFilter));
        }

        private void RefreshCadStatusFilterOptions()
        {
            var previousSelection = _selectedCadStatusFilter;
            CadStatusFilters.Clear();
            CadStatusFilters.Add(L(TranslationKeys.LibraryFilterAllCadStatuses));
            CadStatusFilters.Add(L(TranslationKeys.LibraryFilterCadStatusAvailable));
            CadStatusFilters.Add(L(TranslationKeys.LibraryFilterCadStatusNoCad));
            CadStatusFilters.Add(L(TranslationKeys.LibraryFilterCadStatusNoNativeFile));
            CadStatusFilters.Add(L(TranslationKeys.LibraryFilterCadStatusLookupUnavailable));
            if (!string.IsNullOrWhiteSpace(previousSelection) && CadStatusFilters.Contains(previousSelection))
                _selectedCadStatusFilter = previousSelection;
            else
                _selectedCadStatusFilter = CadStatusFilters[0];
            OnPropertyChanged(nameof(SelectedCadStatusFilter));
        }

        private void RefreshSortOptions()
        {
            var previousSelection = _selectedSortOption;
            SortOptions.Clear();
            SortDirections.Clear();
            SortOptions.Add(L(TranslationKeys.LibrarySortByItemNumber));
            SortOptions.Add(L(TranslationKeys.LibrarySortByName));
            SortOptions.Add(L(TranslationKeys.LibrarySortByEntryStatus));
            SortOptions.Add(L(TranslationKeys.LibrarySortByRevisionPolicy));
            SortOptions.Add(L(TranslationKeys.LibrarySortByCadStatus));
            SortOptions.Add(L(TranslationKeys.LibrarySortByUsageCount));
            SortOptions.Add(L(TranslationKeys.LibrarySortByLastUsedOn));
            SortDirections.Add(L(TranslationKeys.LibrarySortDirectionAscending));
            SortDirections.Add(L(TranslationKeys.LibrarySortDirectionDescending));
            if (!string.IsNullOrWhiteSpace(previousSelection) && SortOptions.Contains(previousSelection))
                _selectedSortOption = previousSelection;
            else
                _selectedSortOption = SortOptions[0];
            _selectedSortDirection = SortDirections[0];
            OnPropertyChanged(nameof(SelectedSortOption));
            OnPropertyChanged(nameof(SelectedSortDirection));
            OnPropertyChanged(nameof(HasSortDirections));
        }

        private static bool? ShowCreateLibraryDialog(CreateLibraryDialogViewModel viewModel)
        {
            var dialog = new CreateLibraryDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.CloseRequested += accepted => dialog.DialogResult = accepted;
            return dialog.ShowDialog();
        }

        private static bool? ShowEditLibraryDialog(EditLibraryDialogViewModel viewModel)
        {
            var dialog = new EditLibraryDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.CloseRequested += accepted => dialog.DialogResult = accepted;
            return dialog.ShowDialog();
        }

        private static bool? ShowPartPickerDialog(ArasPartPickerViewModel viewModel)
        {
            var dialog = new ArasPartPickerDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.CloseRequested += accepted => dialog.DialogResult = accepted;
            return dialog.ShowDialog();
        }

        private static bool? ShowMoveEntryDialog(MoveLibraryEntryDialogViewModel viewModel)
        {
            var dialog = new MoveLibraryEntryDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.CloseRequested += accepted => dialog.DialogResult = accepted;
            return dialog.ShowDialog();
        }

        private static bool? ShowRevisionBrowserDialog(PartRevisionBrowserViewModel viewModel)
        {
            var dialog = new PartRevisionBrowserDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = viewModel
            };
            viewModel.CloseRequested += accepted => dialog.DialogResult = accepted;
            return dialog.ShowDialog();
        }

        private void RefreshFilterOptions()
        {
            var normalizedType = NormalizeTypeFilter(_selectedTypeFilter);
            var normalizedState = NormalizeStateFilter(_selectedStateFilter);
            var normalizedRevision = NormalizeRevisionFilter(_selectedRevisionFilter);

            TypeFilters.Clear();
            TypeFilters.Add(L(TranslationKeys.LibraryFilterAllTypes));
            TypeFilters.Add(L(TranslationKeys.LibraryFilterAssembly));
            TypeFilters.Add(L(TranslationKeys.LibraryFilterComponent));

            StateFilters.Clear();
            StateFilters.Add(L(TranslationKeys.LibraryFilterAllStates));
            StateFilters.Add(L(TranslationKeys.LibraryFilterReleased));
            StateFilters.Add(L(TranslationKeys.LibraryFilterInReview));
            StateFilters.Add(L(TranslationKeys.LibraryFilterPreliminary));

            RevisionFilters.Clear();
            RevisionFilters.Add(L(TranslationKeys.LibraryFilterAllRevisions));
            RevisionFilters.Add(L(TranslationKeys.LibraryFilterLatestReleased));
            RevisionFilters.Add(L(TranslationKeys.LibraryFilterPinned));
            RevisionFilters.Add(L(TranslationKeys.LibraryFilterLatestCurrent));

            _selectedTypeFilter = string.IsNullOrWhiteSpace(normalizedType)
                ? TypeFilters[0]
                : TypeFilters.FirstOrDefault(item => NormalizeTypeFilter(item) == normalizedType) ?? TypeFilters[0];
            _selectedStateFilter = string.IsNullOrWhiteSpace(normalizedState)
                ? StateFilters[0]
                : StateFilters.FirstOrDefault(item => NormalizeStateFilter(item) == normalizedState) ?? StateFilters[0];
            _selectedRevisionFilter = string.IsNullOrWhiteSpace(normalizedRevision)
                ? RevisionFilters[0]
                : RevisionFilters.FirstOrDefault(item => NormalizeRevisionFilter(item) == normalizedRevision) ?? RevisionFilters[0];
        }

        private void OnLocalizationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, "Item[]", StringComparison.Ordinal))
                return;

            RefreshFilterOptions();
            RefreshVisibilityFilterOptions();
            RefreshEntryStatusFilterOptions();
            RefreshCadStatusFilterOptions();
            RefreshSortOptions();
            if (SelectedEntry == null)
                SelectedEntryDetails = CreateEmptyDetails();
            NotifyPanelStateChanged();
            OnPropertyChanged(nameof(SelectedTypeFilter));
            OnPropertyChanged(nameof(SelectedStateFilter));
            OnPropertyChanged(nameof(SelectedRevisionFilter));
            OnPropertyChanged(nameof(ResultSummary));
            OnPropertyChanged(nameof(PagingSummary));
            OnPropertyChanged(nameof(ConnectionTitle));
            OnPropertyChanged(nameof(ConnectionDatabase));
            RaiseCommandStates();
        }

        private static string L(string key)
        {
            return TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, key);
        }

        private static string Lf(string key, params object[] args)
        {
            return string.Format(L(key), args);
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static LibraryRevisionPolicy ParseRevisionPolicy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LibraryRevisionPolicy.LatestCurrent;

            if (Enum.TryParse<LibraryRevisionPolicy>(value, true, out var parsed))
                return parsed;

            return LibraryRevisionPolicy.LatestCurrent;
        }

        private static LibraryEntryStatus ParseEntryStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return LibraryEntryStatus.Draft;

            if (Enum.TryParse<LibraryEntryStatus>(value, true, out var parsed))
                return parsed;

            return LibraryEntryStatus.Draft;
        }
    }

    public sealed class AddLibraryPartToProjectDialogViewModel : INotifyPropertyChanged
    {
        private LibraryParentCandidate _selectedParent;
        private string _quantity = "1";
        private string _workspaceWarning;

        public AddLibraryPartToProjectDialogViewModel(
            PdmProjectsViewModel workspace,
            string partNumber,
            string partName,
            string revision,
            string partId,
            IEnumerable<LibraryParentCandidate> parentCandidates = null)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            PartNumber = partNumber;
            ResolvedPartSummary = string.Format(
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogResolvedPartSummary),
                partName ?? TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryReusablePartFallback),
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogRevisionWord),
                revision ?? "-");
            ReuseBadge = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogReuseBadgeExistingPart);
            RepositoryCode = workspace.SelectedRepository ?? workspace.RepositoryCodeForDisplay;
            BranchName = workspace.SelectedBranch ?? TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogBranchFallbackMain);
            BaseCommitSummary = workspace.LatestCommitSummary;
            ParentCandidates = new ObservableCollection<LibraryParentCandidate>(
                (parentCandidates ?? workspace.GetLibraryParentCandidates() ?? Array.Empty<LibraryParentCandidate>())
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.LogicalCode)));
            SelectedParent = ParentCandidates.FirstOrDefault();
            WorkspaceWarning = workspace.HasUncommittedChanges
                ? TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogUncommittedWarning)
                : TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogStagingWarning);

            PreviewCommand = new RelayCommand(_ => WorkspaceWarning = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogPreviewReady));
            AddCommand = new RelayCommand(_ => ConfirmAdd(), _ => SelectedParent != null && ParsedQuantity > 0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public string PartNumber { get; }
        public string ResolvedPartSummary { get; }
        public string ReuseBadge { get; }
        public string RepositoryCode { get; }
        public string BranchName { get; }
        public string BaseCommitSummary { get; }
        public ObservableCollection<LibraryParentCandidate> ParentCandidates { get; }

        public LibraryParentCandidate SelectedParent
        {
            get => _selectedParent;
            set
            {
                if (_selectedParent == value)
                    return;

                _selectedParent = value;
                OnPropertyChanged();
                (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value)
                    return;

                _quantity = value;
                OnPropertyChanged();
                (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool UseExistingPart { get; set; } = true;
        public bool AddToWorkingTree { get; set; } = true;
        public bool KeepCadReference { get; set; } = true;
        public bool DownloadCadNow { get; set; }
        public bool RecordUsage { get; set; } = true;

        public string WorkspaceWarning
        {
            get => _workspaceWarning;
            set
            {
                if (_workspaceWarning == value)
                    return;
                _workspaceWarning = value;
                OnPropertyChanged();
            }
        }

        public int ParsedQuantity
        {
            get
            {
                if (int.TryParse(Quantity, out var quantity) && quantity > 0)
                    return quantity;

                return 0;
            }
        }

        public ICommand PreviewCommand { get; }
        public ICommand AddCommand { get; }

        private void ConfirmAdd()
        {
            if (SelectedParent == null || ParsedQuantity <= 0)
            {
                WorkspaceWarning = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogValidationMessage);
                return;
            }

            CloseRequested?.Invoke(true);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class LibraryParentCandidate
    {
        public string LogicalCode { get; set; }
        public string DisplayName { get; set; }
    }

    internal sealed class PreviewPartLibraryClient : IPartLibraryClient
    {
        private readonly List<PartLibrarySummary> _libraries;
        private readonly List<PartLibraryEntryDetails> _entries;

        public PreviewPartLibraryClient()
        {
            _libraries = new List<PartLibrarySummary>
            {
                new PartLibrarySummary { Id = "all", Name = "All reusable Parts", LibraryType = LibraryType.Team, ItemCount = 5, CanContribute = true, IsPublic = true },
                new PartLibrarySummary { Id = "mech", Name = "Mechanical Library", LibraryType = LibraryType.Team, ItemCount = 2, CanContribute = true, IsPublic = true },
                new PartLibrarySummary { Id = "hardware", Name = "Hardware Library", LibraryType = LibraryType.Team, ItemCount = 2, CanContribute = true, IsPublic = true },
                new PartLibrarySummary { Id = "standard", Name = "Standard Company Parts", LibraryType = LibraryType.Standard, ItemCount = 1, CanContribute = false, IsPublic = true }
            };

            _entries = new List<PartLibraryEntryDetails>
            {
                CreateEntry("entry-handle", "mech", "Mechanical Library", "PART-HANDLE-A", "CFG-HANDLE", "HANDLE", "Handle", "Component", "A", "Released", LibraryRevisionPolicy.LatestReleased, LibraryEntryStatus.Published, "Available", "HANDLE.ics", 14, false),
                CreateEntry("entry-cover", "mech", "Mechanical Library", "PART-COVER-B", "CFG-COVER", "FRONT-COVER", "Front cover", "Component", "B", "Preliminary", LibraryRevisionPolicy.Pinned, LibraryEntryStatus.Draft, "No CAD", "FRONT-COVER.ics", 2, false),
                CreateEntry("entry-screw", "hardware", "Hardware Library", "PART-SCREW-A", "CFG-SCREW", "SCREW_M5_16", "Socket head screw M5 x 16", "Component", "A", "Released", LibraryRevisionPolicy.LatestReleased, LibraryEntryStatus.Published, "Available", "SCREW_M5_16.ics", 33, false),
                CreateEntry("entry-nut", "hardware", "Hardware Library", "PART-NUT-A", "CFG-NUT", "NUT_M5", "Hex nut M5", "Component", "A", "Released", LibraryRevisionPolicy.LatestReleased, LibraryEntryStatus.Published, "Available", "NUT_M5.ics", 29, false),
                CreateEntry("entry-frame", "standard", "Standard Company Parts", "PART-FRAME-C", "CFG-FRAME", "MAINFRAME", "Body main frame", "Assembly", "C", "Released", LibraryRevisionPolicy.Pinned, LibraryEntryStatus.Published, "Available", "MAINFRAME.ics", 5, true)
            };
        }

        public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(
            LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<PartLibrarySummary>)_libraries.ToList());
        }

        public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
        {
            IEnumerable<PartLibraryEntryDetails> query = _entries;

            if (!string.IsNullOrWhiteSpace(request.LibraryId) && !string.Equals(request.LibraryId, "all", StringComparison.OrdinalIgnoreCase))
                query = query.Where(entry => string.Equals(entry.LibraryId, request.LibraryId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                query = query.Where(entry =>
                    Contains(entry.PartNumber, request.SearchText) ||
                    Contains(entry.PartName, request.SearchText) ||
                    Contains(entry.Description, request.SearchText));
            }

            if (!string.IsNullOrWhiteSpace(request.TypeFilter))
                query = query.Where(entry => string.Equals(entry.PartType, request.TypeFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.StateFilter))
                query = query.Where(entry => string.Equals(entry.LifecycleState, request.StateFilter, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.RevisionFilter))
            {
                query = query.Where(entry => string.Equals(entry.RevisionPolicy.ToString(), request.RevisionFilter.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.RevisionPolicy.ToString(), request.RevisionFilter, StringComparison.OrdinalIgnoreCase));
            }

            var entries = query
                .OrderBy(entry => entry.PartNumber, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new PartLibraryEntrySummary
                {
                    EntryId = entry.EntryId,
                    LibraryId = entry.LibraryId,
                    LibraryName = entry.LibraryName,
                    PartId = entry.PartId,
                    PartConfigId = entry.PartConfigId,
                    PartNumber = entry.PartNumber,
                    PartName = entry.PartName,
                    PartType = entry.PartType,
                    Revision = entry.Revision,
                    LifecycleState = entry.LifecycleState,
                    RevisionPolicy = entry.RevisionPolicy,
                    EntryStatus = entry.EntryStatus,
                    CadStatus = entry.CadStatus,
                    UsageCount = entry.UsageCount,
                    HasNewerReleasedRevision = entry.HasNewerReleasedRevision,
                    IsDeprecated = entry.EntryStatus == LibraryEntryStatus.Deprecated
                })
                .ToList();

            return Task.FromResult(new PartLibrarySearchResponse
            {
                Entries = entries,
                TotalCount = entries.Count,
                PageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber,
                PageSize = request.PageSize <= 0 ? entries.Count : request.PageSize
            });
        }

        public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries.First(entry => string.Equals(entry.EntryId, entryId, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AddPartToLibraryResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistSave)
            });
        }

        public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new MoveLibraryEntryResult
            {
                Success = false,
                EntryId = request?.EntryId,
                TargetLibraryId = request?.TargetLibraryId,
                ErrorCode = ArasErrorCode.ValidationFailed,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistMove)
            });

        public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
        {
            var entry = _entries.First(item => string.Equals(item.EntryId, entryId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(new ResolveLibraryPartResult
            {
                EntryId = entry.EntryId,
                ResolvedPartId = entry.PartId,
                ResolvedPartConfigId = entry.PartConfigId,
                ResolvedRevision = entry.Revision,
                LifecycleState = entry.LifecycleState,
                CadStatus = entry.CadStatus,
                HasNewerReleasedRevision = entry.HasNewerReleasedRevision
            });
        }

        public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new PartRevisionHistoryResponse
            {
                Items = Array.Empty<PartRevisionHistoryItem>(),
                PageNumber = request?.PageNumber ?? 1,
                PageSize = request?.PageSize ?? 25,
                TotalCount = 0
            });

        public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());
        public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RecordLibraryUsageResult { Success = true });
        public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
        {
            return ResolvePartAsync(entryId, LibraryRevisionPolicy.LatestReleased, cancellationToken);
        }
        public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UpdateLibraryRevisionPolicyResult
            {
                Success = false,
                EntryId = request?.EntryId,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistPolicy)
            });
        }
        public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryMutationResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistCreate)
            });

        public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryMutationResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistUpdate)
            });

        public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryMutationResult
            {
                Success = false,
                ErrorMessage = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewNoPersistArchive)
            });

        public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new PartPickerSearchResponse
            {
                Items = Array.Empty<PartPickerSearchResultItem>(),
                TotalCount = 0,
                PageNumber = request?.PageNumber ?? 1,
                PageSize = request?.PageSize ?? 25
            });

        public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
            => Task.FromResult(new PartPreview
            {
                PartId = partId,
                IsEligibleForReuse = false,
                IneligibilityReason = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryPreviewMode)
            });

        public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
            => Task.FromResult(new DuplicateEntryCheckResult { IsDuplicate = false });

        public Task<LibraryEntryCadDetails> GetCadDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryEntryCadDetails());
        public Task<LibraryEntryBomDetails> GetBomDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryEntryBomDetails());
        public Task<LibraryEntryRevisionDetails> GetRevisionDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryEntryRevisionDetails());
        public Task<LibraryEntryWhereUsedDetails> GetWhereUsedDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryEntryWhereUsedDetails());
        public Task<LibraryEntryDetailBundle> GetDetailBundleAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new LibraryEntryDetailBundle());

        public void Dispose() { }

        private static bool Contains(string value, string keyword)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(keyword ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static PartLibraryEntryDetails CreateEntry(
            string entryId,
            string libraryId,
            string libraryName,
            string partId,
            string partConfigId,
            string partNumber,
            string partName,
            string partType,
            string revision,
            string lifecycleState,
            LibraryRevisionPolicy policy,
            LibraryEntryStatus status,
            string cadStatus,
            string cadFileName,
            int usageCount,
            bool newerRevision)
        {
            return new PartLibraryEntryDetails
            {
                EntryId = entryId,
                LibraryId = libraryId,
                LibraryName = libraryName,
                PartId = partId,
                PartConfigId = partConfigId,
                PartNumber = partNumber,
                PartName = partName,
                PartType = partType,
                Revision = revision,
                LifecycleState = lifecycleState,
                RevisionPolicy = policy,
                EntryStatus = status,
                CadStatus = cadStatus,
                PrimaryCadId = "CAD-" + partNumber,
                PrimaryCadFileName = cadFileName,
                PrimaryCadState = cadStatus,
                LockedBy = string.Empty,
                UsageCount = usageCount,
                Description = partName,
                HasNewerReleasedRevision = newerRevision
            };
        }
    }

    internal sealed class UnavailablePartLibraryClient : IPartLibraryClient
    {
        public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(
            LibraryVisibilityFilter visibilityFilter = LibraryVisibilityFilter.Active,
            CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyList<PartLibrarySummary>)Array.Empty<PartLibrarySummary>());

        public Task<PartLibrarySearchResponse> SearchEntriesAsync(PartLibrarySearchRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new PartLibrarySearchResponse
            {
                Entries = Array.Empty<PartLibraryEntrySummary>(),
                TotalCount = 0,
                PageNumber = request?.PageNumber <= 0 ? 1 : request.PageNumber,
                PageSize = request?.PageSize <= 0 ? 25 : request.PageSize
            });

        public Task<PartLibraryEntryDetails> GetEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromResult(new PartLibraryEntryDetails());

        public Task<AddPartToLibraryResult> AddPartAsync(AddPartToLibraryRequest request, CancellationToken cancellationToken)
            => Task.FromException<AddPartToLibraryResult>(CreateUnavailableException());

        public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task<MoveLibraryEntryResult> MoveLibraryEntryAsync(MoveLibraryEntryRequest request, CancellationToken cancellationToken)
            => Task.FromException<MoveLibraryEntryResult>(CreateUnavailableException());

        public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
            => Task.FromException<ResolveLibraryPartResult>(CreateUnavailableException());

        public Task<PartRevisionHistoryResponse> SearchPartRevisionsAsync(PartRevisionHistoryRequest request, CancellationToken cancellationToken)
            => Task.FromException<PartRevisionHistoryResponse>(CreateUnavailableException());

        public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<PartWhereUsedItem>>(CreateUnavailableException());

        public Task<RecordLibraryUsageResult> RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
            => Task.FromException<RecordLibraryUsageResult>(CreateUnavailableException());

        public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<ResolveLibraryPartResult>(CreateUnavailableException());

        public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
            => Task.FromException<UpdateLibraryRevisionPolicyResult>(CreateUnavailableException());

        public Task<LibraryMutationResult> CreateLibraryAsync(CreatePartLibraryRequest request, CancellationToken cancellationToken)
            => Task.FromException<LibraryMutationResult>(CreateUnavailableException());

        public Task<LibraryMutationResult> UpdateLibraryAsync(UpdatePartLibraryRequest request, CancellationToken cancellationToken)
            => Task.FromException<LibraryMutationResult>(CreateUnavailableException());

        public Task<LibraryMutationResult> ArchiveLibraryAsync(string libraryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryMutationResult>(CreateUnavailableException());

        public Task<PartPickerSearchResponse> SearchPartsAsync(PartPickerSearchRequest request, CancellationToken cancellationToken)
            => Task.FromException<PartPickerSearchResponse>(CreateUnavailableException());

        public Task<PartPreview> GetPartPreviewAsync(string partId, CancellationToken cancellationToken)
            => Task.FromException<PartPreview>(CreateUnavailableException());

        public Task<DuplicateEntryCheckResult> CheckDuplicateEntryAsync(string libraryId, string partConfigId, CancellationToken cancellationToken)
            => Task.FromException<DuplicateEntryCheckResult>(CreateUnavailableException());

        public Task<LibraryEntryCadDetails> GetCadDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryEntryCadDetails>(CreateUnavailableException());
        public Task<LibraryEntryBomDetails> GetBomDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryEntryBomDetails>(CreateUnavailableException());
        public Task<LibraryEntryRevisionDetails> GetRevisionDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryEntryRevisionDetails>(CreateUnavailableException());
        public Task<LibraryEntryWhereUsedDetails> GetWhereUsedDetailsAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryEntryWhereUsedDetails>(CreateUnavailableException());
        public Task<LibraryEntryDetailBundle> GetDetailBundleAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<LibraryEntryDetailBundle>(CreateUnavailableException());

        public void Dispose() { }

        private static Exception CreateUnavailableException()
        {
            return new ArasOperationException(
                ArasErrorCode.AuthInvalid,
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryUnavailableSignInMessage));
        }
    }
}
