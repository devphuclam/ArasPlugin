using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using IdeaCadConnector.Desktop.Services;
using IdeaCadConnector.Workspace;

namespace IdeaCadConnector.Desktop
{
    public sealed class LibraryViewModel : ILibraryViewModel
    {
        private readonly IAppSessionContext _session;
        private readonly IPartLibraryClient _client;
        private PartLibrarySummaryRow _selectedLibrary;
        private PartLibraryEntryRow _selectedEntry;
        private PartLibraryEntryDetailsView _selectedEntryDetails;
        private string _searchText;
        private string _selectedTypeFilter;
        private string _selectedStateFilter;
        private string _selectedRevisionFilter;
        private bool _isLoading;
        private string _statusMessage;
        private int _totalCount;
        private int _pageNumber = 1;
        private int _pageSize = 25;

        public LibraryViewModel()
            : this(AppSessionContext.Current, AppSessionContext.Current.PartLibraryClient ?? new UnavailablePartLibraryClient())
        {
        }

        public LibraryViewModel(IAppSessionContext session, IPartLibraryClient client)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _client = client ?? throw new ArgumentNullException(nameof(client));

            Libraries = new ObservableCollection<PartLibrarySummaryRow>();
            Entries = new ObservableCollection<PartLibraryEntryRow>();
            TypeFilters = new ObservableCollection<string>(new[] { "All Types", "Assembly", "Component" });
            StateFilters = new ObservableCollection<string>(new[] { "All States", "Released", "In Review", "Preliminary", "Deprecated" });
            RevisionFilters = new ObservableCollection<string>(new[] { "All Revisions", "Latest Released", "Pinned", "Latest Current" });

            _selectedTypeFilter = TypeFilters[0];
            _selectedStateFilter = StateFilters[0];
            _selectedRevisionFilter = RevisionFilters[0];
            _selectedEntryDetails = new PartLibraryEntryDetailsView();
            _statusMessage = "Ready.";

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsLoading);
            SearchCommand = new RelayCommand(_ => _ = SearchAsync(), _ => !IsLoading);
            CreateLibraryCommand = new RelayCommand(_ => StatusMessage = "Library creation requires the Aras Library backend configuration.");
            AddPartCommand = new RelayCommand(_ => ShowSaveToLibraryDialog());
            RemoveEntryCommand = new RelayCommand(_ => StatusMessage = "Remove Entry will be enabled when the server Library relationship API is available.", _ => SelectedEntry != null);
            MoveEntryCommand = new RelayCommand(_ => StatusMessage = "Move Entry will be enabled when the server Library relationship API is available.", _ => SelectedEntry != null);
            AddToCurrentProjectCommand = new RelayCommand(_ => _ = AddToCurrentProjectAsync(), _ => SelectedEntry != null && HasActivePdmWorkspace && !IsLoading && !SelectedEntry.IsDeprecated);
            OpenInIronCadCommand = new RelayCommand(_ => StatusMessage = "Open in IronCAD will reuse the existing CAD open flow in the next slice.", _ => SelectedEntry != null);
            DownloadCadCommand = new RelayCommand(_ => StatusMessage = "Download CAD is not wired yet.", _ => SelectedEntry != null);
            PublishCommand = new RelayCommand(_ => new PublishLibraryEntryDialog { Owner = Application.Current?.MainWindow }.ShowDialog(), _ => SelectedEntry != null);
            DeprecateCommand = new RelayCommand(_ => StatusMessage = "Deprecate Entry will be enabled when the server Library workflow is available.", _ => SelectedEntry != null);
            PinRevisionCommand = new RelayCommand(_ => StatusMessage = "Revision policy editing will be enabled with the server Library client.", _ => SelectedEntry != null);
            UseLatestReleasedCommand = new RelayCommand(_ => StatusMessage = "Revision policy editing will be enabled with the server Library client.", _ => SelectedEntry != null);
            ViewWhereUsedCommand = new RelayCommand(_ => _ = ViewWhereUsedAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenInArasCommand = new RelayCommand(_ => StatusMessage = "Open in Aras will be wired once the Library entry details map to live Aras items.", _ => SelectedEntry != null);

            _ = RefreshAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<PartLibrarySummaryRow> Libraries { get; }

        public ObservableCollection<PartLibraryEntryRow> Entries { get; }

        public ObservableCollection<string> TypeFilters { get; }

        public ObservableCollection<string> StateFilters { get; }

        public ObservableCollection<string> RevisionFilters { get; }

        public PartLibrarySummaryRow SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetField(ref _selectedLibrary, value))
                    _ = SearchAsync();
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

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public string ResultSummary => _totalCount <= 0
            ? "No reusable Parts found"
            : _totalCount + " reusable Parts";

        public string PagingSummary
        {
            get
            {
                if (_totalCount <= 0)
                    return "0 results";

                var start = ((_pageNumber - 1) * _pageSize) + 1;
                var end = Math.Min(_totalCount, _pageNumber * _pageSize);
                return "Showing " + start + "-" + end + " of " + _totalCount;
            }
        }

        public string ConnectionTitle => _session.IsConnected
            ? "Connected as " + (_session.CurrentUserName ?? "engineer")
            : "Offline";

        public string ConnectionDatabase => _session.IsConnected
            ? "Library workspace active"
            : "Connect to Aras to reuse managed Parts";

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand CreateLibraryCommand { get; }
        public ICommand AddPartCommand { get; }
        public ICommand RemoveEntryCommand { get; }
        public ICommand MoveEntryCommand { get; }
        public ICommand AddToCurrentProjectCommand { get; }
        public ICommand OpenInIronCadCommand { get; }
        public ICommand DownloadCadCommand { get; }
        public ICommand PublishCommand { get; }
        public ICommand DeprecateCommand { get; }
        public ICommand PinRevisionCommand { get; }
        public ICommand UseLatestReleasedCommand { get; }
        public ICommand ViewWhereUsedCommand { get; }
        public ICommand OpenInArasCommand { get; }

        private async Task RefreshAsync()
        {
            await RunBusyAsync(async () =>
            {
                var libraries = await _client.GetLibrariesAsync(CancellationToken.None).ConfigureAwait(true);
                Libraries.Clear();
                foreach (var library in libraries)
                {
                    Libraries.Add(new PartLibrarySummaryRow
                    {
                        Id = library.Id,
                        Name = library.Name,
                        ItemCount = library.ItemCount,
                        LibraryType = library.LibraryType.ToString(),
                        CanContribute = library.CanContribute
                    });
                }

                if (Libraries.Count > 0)
                    SelectedLibrary = Libraries[0];
                else
                    await SearchAsync().ConfigureAwait(true);

                StatusMessage = Libraries.Count > 0
                    ? "Library data refreshed."
                    : "No accessible Libraries were returned.";
            });
        }

        private async Task SearchAsync()
        {
            await RunBusyAsync(async () =>
            {
                var response = await _client.SearchEntriesAsync(new PartLibrarySearchRequest
                {
                    LibraryId = SelectedLibrary?.Id,
                    SearchText = SearchText,
                    TypeFilter = NormalizeFilter(SelectedTypeFilter),
                    StateFilter = NormalizeFilter(SelectedStateFilter),
                    RevisionFilter = NormalizeFilter(SelectedRevisionFilter),
                    PageNumber = _pageNumber,
                    PageSize = _pageSize
                }, CancellationToken.None).ConfigureAwait(true);

                Entries.Clear();
                foreach (var entry in response.Entries ?? Array.Empty<PartLibraryEntrySummary>())
                {
                    Entries.Add(MapEntry(entry));
                }

                _totalCount = response.TotalCount;
                _pageNumber = response.PageNumber <= 0 ? 1 : response.PageNumber;
                _pageSize = response.PageSize <= 0 ? 25 : response.PageSize;
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(PagingSummary));

                if (Entries.Count > 0)
                    SelectedEntry = Entries[0];
                else
                    SelectedEntryDetails = new PartLibraryEntryDetailsView();

                StatusMessage = Entries.Count > 0
                    ? "Library results updated."
                    : "No Library entries matched the current filters.";
            });
        }

        private async Task LoadSelectedEntryAsync()
        {
            if (SelectedEntry == null)
            {
                SelectedEntryDetails = new PartLibraryEntryDetailsView();
                return;
            }

            var details = await _client.GetEntryAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
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
                LifecycleState = details.LifecycleState,
                RevisionPolicy = details.RevisionPolicy.ToString(),
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
                LockedBy = details.LockedBy,
                UsageCount = details.UsageCount,
                CadStatus = details.CadStatus,
                WhereUsedSummary = "Select \"View Where Used\" to load live reuse information."
            };
        }

        private async Task ViewWhereUsedAsync()
        {
            if (SelectedEntry == null)
            {
                StatusMessage = "Select a Library Part first.";
                return;
            }

            var partId = SelectedEntryDetails?.PartId ?? SelectedEntry.PartId;
            if (string.IsNullOrWhiteSpace(partId))
            {
                StatusMessage = "This Library entry does not have a resolved Aras Part ID yet.";
                return;
            }

            await RunBusyAsync(async () =>
            {
                var whereUsed = await _client.GetWhereUsedAsync(partId, CancellationToken.None).ConfigureAwait(true);
                var summary = BuildWhereUsedSummary(whereUsed);
                SelectedEntryDetails = CloneDetailsWithWhereUsed(SelectedEntryDetails, summary);
                StatusMessage = whereUsed.Count == 0
                    ? "Where Used returned no parent Parts."
                    : "Where Used loaded " + whereUsed.Count + " parent Part(s).";
            });
        }

        private async Task AddToCurrentProjectAsync()
        {
            var workspace = _session.CurrentPdmProjectsViewModel;
            if (workspace == null || SelectedEntry == null)
            {
                StatusMessage = "Open a PDM workspace before reusing a Library Part.";
                return;
            }

            var dialogViewModel = new AddLibraryPartToProjectDialogViewModel(
                workspace,
                SelectedEntryDetails.PartNumber ?? SelectedEntry.PartNumber,
                SelectedEntryDetails.PartName ?? SelectedEntry.PartName,
                SelectedEntryDetails.Revision ?? SelectedEntry.Revision,
                SelectedEntryDetails.PartId ?? SelectedEntry.PartId);

            var dialog = new AddLibraryPartToProjectDialog
            {
                Owner = Application.Current?.MainWindow,
                DataContext = dialogViewModel
            };

            dialogViewModel.CloseRequested += accepted =>
            {
                dialog.DialogResult = accepted;
                dialog.Close();
            };

            if (dialog.ShowDialog() != true)
                return;

            var reference = new WorkspaceLibraryReference
            {
                ReferenceId = Guid.NewGuid().ToString("N"),
                LibraryId = SelectedEntry.LibraryId,
                LibraryEntryId = SelectedEntry.EntryId,
                PartId = SelectedEntry.PartId,
                PartConfigId = SelectedEntry.PartConfigId,
                PartNumber = SelectedEntry.PartNumber,
                PartName = SelectedEntry.PartName,
                Revision = SelectedEntry.Revision,
                ParentLogicalCode = dialogViewModel.SelectedParent.LogicalCode,
                LocalLogicalCode = "LIB-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant(),
                Quantity = dialogViewModel.ParsedQuantity,
                RevisionPolicy = SelectedEntry.RevisionPolicy,
                AddedOn = DateTime.UtcNow,
                AddedBy = _session.CurrentUserName ?? "engineer"
            };

            var result = workspace.AddLibraryReference(reference);
            StatusMessage = result.Message;

            if (result.Success)
                await SearchAsync().ConfigureAwait(true);
        }

        private void ShowSaveToLibraryDialog()
        {
            new SaveToLibraryDialog
            {
                Owner = Application.Current?.MainWindow
            }.ShowDialog();
            StatusMessage = "Save-to-Library UI opened.";
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
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                StatusMessage = "Part Library failed: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string NormalizeFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
                return null;

            return value;
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
                RevisionPolicy = entry.RevisionPolicy.ToString(),
                CadStatus = entry.CadStatus,
                UsageCount = entry.UsageCount,
                HasNewerReleasedRevision = entry.HasNewerReleasedRevision,
                IsDeprecated = entry.IsDeprecated,
                LibraryName = entry.LibraryName
            };
        }

        private static string BuildWhereUsedSummary(IReadOnlyList<PartWhereUsedItem> whereUsed)
        {
            if (whereUsed == null || whereUsed.Count == 0)
                return "No parent Parts are currently reusing this Part.";

            var lines = whereUsed
                .Take(6)
                .Select(item =>
                {
                    var parent = item.ParentPartNumber ?? item.ParentPartName ?? "Unknown parent";
                    var name = string.IsNullOrWhiteSpace(item.ParentPartName) ? string.Empty : " - " + item.ParentPartName;
                    var quantity = item.Quantity > 0 ? " (qty " + item.Quantity + ")" : string.Empty;
                    return parent + name + quantity;
                })
                .ToList();

            if (whereUsed.Count > lines.Count)
                lines.Add("...and " + (whereUsed.Count - lines.Count) + " more parent Part(s).");

            return string.Join(Environment.NewLine, lines);
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
                LifecycleState = details.LifecycleState,
                RevisionPolicy = details.RevisionPolicy,
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
                LockedBy = details.LockedBy,
                UsageCount = details.UsageCount,
                CadStatus = details.CadStatus,
                WhereUsedSummary = whereUsedSummary
            };
        }

        private void RaiseCommandStates()
        {
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveEntryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddToCurrentProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenInIronCadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PublishCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeprecateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PinRevisionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (UseLatestReleasedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ViewWhereUsedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenInArasCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
    }

    public sealed class AddLibraryPartToProjectDialogViewModel : INotifyPropertyChanged
    {
        private LibraryParentCandidate _selectedParent;
        private string _quantity = "1";
        private string _workspaceWarning;

        public AddLibraryPartToProjectDialogViewModel(PdmProjectsViewModel workspace, string partNumber, string partName, string revision, string partId)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            PartNumber = partNumber;
            ResolvedPartSummary = (partName ?? "Reusable Part") + " • Revision " + (revision ?? "-");
            ReuseBadge = "Existing Aras Part";
            RepositoryCode = workspace.SelectedRepository ?? workspace.RepositoryCodeForDisplay;
            BranchName = workspace.SelectedBranch ?? "main";
            BaseCommitSummary = workspace.LatestCommitSummary;
            ParentCandidates = new ObservableCollection<LibraryParentCandidate>(workspace.GetLibraryParentCandidates());
            SelectedParent = ParentCandidates.FirstOrDefault();
            WorkspaceWarning = workspace.HasUncommittedChanges
                ? "Uncommitted changes detected. The reusable Part will be added to the current working tree and should be committed locally."
                : "The reusable Part will be staged in the working tree and included in the next local commit.";

            PreviewCommand = new RelayCommand(_ => WorkspaceWarning = "Preview ready: the Part will be added as a local Library reference and reused on Push.");
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
                WorkspaceWarning = "Select a target parent and enter a quantity greater than 0.";
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

        public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(CancellationToken cancellationToken)
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
                ErrorMessage = "Preview client does not persist Save-to-Library yet."
            });
        }

        public Task RemoveEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MoveEntryAsync(string entryId, string targetLibraryId, CancellationToken cancellationToken) => Task.CompletedTask;

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

        public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<PartWhereUsedItem>)Array.Empty<PartWhereUsedItem>());
        public Task RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
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
        public Task<IReadOnlyList<PartLibrarySummary>> GetLibrariesAsync(CancellationToken cancellationToken)
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

        public Task<ResolveLibraryPartResult> ResolvePartAsync(string entryId, LibraryRevisionPolicy policy, CancellationToken cancellationToken)
            => Task.FromException<ResolveLibraryPartResult>(CreateUnavailableException());

        public Task PublishEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task DeprecateEntryAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public Task<IReadOnlyList<PartWhereUsedItem>> GetWhereUsedAsync(string partId, CancellationToken cancellationToken)
            => Task.FromException<IReadOnlyList<PartWhereUsedItem>>(CreateUnavailableException());

        public Task RecordUsageAsync(LibraryUsageRequest request, CancellationToken cancellationToken)
            => Task.FromException(CreateUnavailableException());

        public void Dispose() { }

        private static Exception CreateUnavailableException()
        {
            return new ArasOperationException(
                ArasErrorCode.AuthInvalid,
                "Part Library is not available until you sign in to Aras.");
        }
    }
}
