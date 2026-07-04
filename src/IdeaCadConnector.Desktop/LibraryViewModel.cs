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
        private readonly IAppSessionContext _session;
        private readonly IPartLibraryClient _injectedClient;
        private readonly IPartLibraryClient _unavailableClient = new UnavailablePartLibraryClient();
        private PartLibrarySummaryRow _selectedLibrary;
        private PartLibraryEntryRow _selectedEntry;
        private PartLibraryEntryDetailsView _selectedEntryDetails;
        private string _searchText;
        private string _selectedTypeFilter;
        private string _selectedStateFilter;
        private string _selectedRevisionFilter;
        private bool _isLoading;
        private string _statusMessage;
        private string _errorMessage;
        private string _permissionMessage;
        private int _totalCount;
        private int _pageNumber = 1;
        private int _pageSize = 25;

        public LibraryViewModel()
            : this(AppSessionContext.Current, null)
        {
        }

        public LibraryViewModel(IAppSessionContext session, IPartLibraryClient client)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _injectedClient = client;

            Libraries = new ObservableCollection<PartLibrarySummaryRow>();
            Entries = new ObservableCollection<PartLibraryEntryRow>();
            TypeFilters = new ObservableCollection<string>();
            StateFilters = new ObservableCollection<string>();
            RevisionFilters = new ObservableCollection<string>();
            RefreshFilterOptions();

            _selectedTypeFilter = TypeFilters[0];
            _selectedStateFilter = StateFilters[0];
            _selectedRevisionFilter = RevisionFilters[0];
            _selectedEntryDetails = CreateEmptyDetails();
            _statusMessage = L(TranslationKeys.LibraryStatusReady);
            _permissionMessage = string.Empty;
            _errorMessage = string.Empty;

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsLoading);
            SearchCommand = new RelayCommand(_ => _ = SearchAsync(), _ => !IsLoading);
            CreateLibraryCommand = new RelayCommand(_ => ShowCreateLibraryNotAvailableMessage(), _ => !IsLoading && !IsOffline);
            AddPartCommand = new RelayCommand(_ => ShowSaveToLibraryDialog(), _ => !IsLoading && !IsOffline && CanContributeToSelectedLibrary);
            RemoveEntryCommand = new RelayCommand(_ => _ = RemoveSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading);
            MoveEntryCommand = new RelayCommand(_ => _ = MoveSelectedEntryAsync(), _ => SelectedEntry != null && SelectedLibrary != null && !IsLoading);
            AddToCurrentProjectCommand = new RelayCommand(_ => _ = AddToCurrentProjectAsync(), _ => SelectedEntry != null && HasActivePdmWorkspace && !IsLoading && !SelectedEntry.IsDeprecated && SelectedEntry.CanAddToProject && !SelectedEntry.ResolutionFailed);
            OpenInIronCadCommand = new RelayCommand(_ => _ = OpenPrimaryCadAsync(), _ => SelectedEntry != null && !IsLoading);
            DownloadCadCommand = new RelayCommand(_ => _ = RevealPrimaryCadAsync(), _ => SelectedEntry != null && !IsLoading);
            PublishCommand = new RelayCommand(_ => _ = PublishSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading && !SelectedEntry.IsDeprecated);
            DeprecateCommand = new RelayCommand(_ => _ = DeprecateSelectedEntryAsync(), _ => SelectedEntry != null && !IsLoading && !SelectedEntry.IsDeprecated);
            PinRevisionCommand = new RelayCommand(_ => _ = ResolveSelectedEntryAsync(LibraryRevisionPolicy.Pinned), _ => SelectedEntry != null && !IsLoading);
            UseLatestReleasedCommand = new RelayCommand(_ => _ = ResolveSelectedEntryAsync(LibraryRevisionPolicy.LatestReleased), _ => SelectedEntry != null && !IsLoading);
            ViewWhereUsedCommand = new RelayCommand(_ => _ = ViewWhereUsedAsync(), _ => SelectedEntry != null && !IsLoading);
            OpenInArasCommand = new RelayCommand(_ => OpenInAras(), _ => SelectedEntry != null);

            LocalizationSource.Instance.PropertyChanged += OnLocalizationChanged;
            _session.LibraryDataChanged += OnLibraryDataChanged;

            _ = RefreshAsync();
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

        public PartLibrarySummaryRow SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetField(ref _selectedLibrary, value))
                {
                    OnPropertyChanged(nameof(CanContributeToSelectedLibrary));
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

        public bool CanContributeToSelectedLibrary => SelectedLibrary != null && SelectedLibrary.CanContribute;

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
            ? Lf(TranslationKeys.LibraryConnectionConnectedAs, _session.CurrentUserName ?? "engineer")
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
                var libraries = await ActiveClient.GetLibrariesAsync(CancellationToken.None).ConfigureAwait(true);
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

                _totalCount = response.TotalCount;
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
                return;
            }

            var details = await ActiveClient.GetEntryAsync(SelectedEntry.EntryId, CancellationToken.None).ConfigureAwait(true);
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
                EntryLifecycleState = details.EntryLifecycleState,
                RevisionPolicy = details.RevisionPolicy.ToString(),
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
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
            var workspace = _session.CurrentPdmProjectsViewModel;
            if (workspace == null || SelectedEntry == null)
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

            var dialogViewModel = new AddLibraryPartToProjectDialogViewModel(
                workspace,
                resolvedPartNumber,
                resolvedPartName,
                resolvedRevision,
                resolvedPartId);

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
                AddedBy = _session.CurrentUserName ?? "engineer"
            };

            var result = workspace.AddLibraryReference(reference);
            StatusMessage = result.Message;

            if (result.Success)
                await SearchAsync().ConfigureAwait(true);
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

        private void ShowCreateLibraryNotAvailableMessage()
        {
            StatusMessage = L(TranslationKeys.LibraryStatusCreateLibraryUnavailable);
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

        private async Task MoveSelectedEntryAsync()
        {
            if (SelectedEntry == null || SelectedLibrary == null)
                return;

            if (string.Equals(SelectedEntry.LibraryId, SelectedLibrary.Id, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusSelectDifferentLibrary);
                return;
            }

            var confirm = MessageBox.Show(
                L(TranslationKeys.LibraryConfirmMoveEntry),
                L(TranslationKeys.LibraryDialogActionTitle),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunBusyAsync(async () =>
            {
                await ActiveClient.MoveEntryAsync(SelectedEntry.EntryId, SelectedLibrary.Id, CancellationToken.None).ConfigureAwait(true);
                StatusMessage = L(TranslationKeys.LibraryStatusEntryMoved);
                await RefreshAsync().ConfigureAwait(true);
            });
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

        private async Task OpenPrimaryCadAsync()
        {
            var fileName = SelectedEntryDetails?.PrimaryCadFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusNoPrimaryCadFileName);
                return;
            }

            var resolved = FindWorkspaceCadFile(fileName);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadNotFound);
                return;
            }

            await Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = resolved,
                    UseShellExecute = true
                });
            }).ConfigureAwait(true);

            StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadOpened);
        }

        private async Task RevealPrimaryCadAsync()
        {
            var fileName = SelectedEntryDetails?.PrimaryCadFileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusNoPrimaryCadFileName);
                return;
            }

            var resolved = FindWorkspaceCadFile(fileName);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadNotFound);
                return;
            }

            var folder = Path.GetDirectoryName(resolved);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadFolderOpenFailed);
                return;
            }

            await Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }).ConfigureAwait(true);

            StatusMessage = L(TranslationKeys.LibraryStatusPrimaryCadFolderOpened);
        }

        private void OpenInAras()
        {
            var partId = SelectedEntryDetails?.PartId ?? SelectedEntry?.PartId;
            if (string.IsNullOrWhiteSpace(partId))
            {
                StatusMessage = L(TranslationKeys.LibraryStatusNoResolvedPartId);
                return;
            }

            StatusMessage = L(TranslationKeys.LibraryStatusOpenInArasRequiresUrl);
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
                return "Assembly";
            if (value == L(TranslationKeys.LibraryFilterComponent))
                return "Component";
            return value;
        }

        private string NormalizeStateFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == L(TranslationKeys.LibraryFilterAllStates))
                return null;
            if (value == L(TranslationKeys.LibraryFilterReleased))
                return "Released";
            if (value == L(TranslationKeys.LibraryFilterInReview))
                return "In Review";
            if (value == L(TranslationKeys.LibraryFilterPreliminary))
                return "Preliminary";
            if (value == L(TranslationKeys.LibraryFilterDeprecated))
                return "Deprecated";
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
                EntryLifecycleState = details.EntryLifecycleState,
                RevisionPolicy = details.RevisionPolicy,
                PrimaryCadId = details.PrimaryCadId,
                PrimaryCadFileName = details.PrimaryCadFileName,
                PrimaryCadState = details.PrimaryCadState,
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
            (AddPartCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        private void ClearTransientStates()
        {
            _permissionMessage = string.Empty;
            _errorMessage = string.Empty;
            NotifyPanelStateChanged();
        }

        private void NotifyPanelStateChanged()
        {
            OnPropertyChanged(nameof(CanContributeToSelectedLibrary));
            OnPropertyChanged(nameof(IsPermissionState));
            OnPropertyChanged(nameof(IsErrorState));
            OnPropertyChanged(nameof(ShowLibrariesOverlay));
            OnPropertyChanged(nameof(ShowEntriesOverlay));
            OnPropertyChanged(nameof(LibrariesOverlayMessage));
            OnPropertyChanged(nameof(EntriesOverlayMessage));
            OnPropertyChanged(nameof(AddToProjectHint));
        }

        private void OnLibraryDataChanged(object sender, EventArgs e)
        {
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
            StateFilters.Add(L(TranslationKeys.LibraryFilterDeprecated));

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
            ResolvedPartSummary = string.Format(
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogResolvedPartSummary),
                partName ?? "Reusable Part",
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogRevisionWord),
                revision ?? "-");
            ReuseBadge = TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogReuseBadgeExistingPart);
            RepositoryCode = workspace.SelectedRepository ?? workspace.RepositoryCodeForDisplay;
            BranchName = workspace.SelectedBranch ?? TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryAddDialogBranchFallbackMain);
            BaseCommitSummary = workspace.LatestCommitSummary;
            ParentCandidates = new ObservableCollection<LibraryParentCandidate>(workspace.GetLibraryParentCandidates());
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
                ErrorMessage = "Preview client does not persist policy changes."
            });
        }
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

        public Task<ResolveLibraryPartResult> ResolveUsingStoredPolicyAsync(string entryId, CancellationToken cancellationToken)
            => Task.FromException<ResolveLibraryPartResult>(CreateUnavailableException());

        public Task<UpdateLibraryRevisionPolicyResult> UpdateRevisionPolicyAsync(UpdateLibraryRevisionPolicyRequest request, CancellationToken cancellationToken)
            => Task.FromException<UpdateLibraryRevisionPolicyResult>(CreateUnavailableException());

        public void Dispose() { }

        private static Exception CreateUnavailableException()
        {
            return new ArasOperationException(
                ArasErrorCode.AuthInvalid,
                TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, TranslationKeys.LibraryUnavailableSignInMessage));
        }
    }
}
