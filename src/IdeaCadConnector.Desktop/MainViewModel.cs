using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Ui.ViewModels;
using IdeaCadConnector.Workspace;

namespace IdeaCadConnector.Desktop
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly LoginViewModel _loginViewModel;
        private readonly WorkspaceService _workspaceService;
        private readonly ICadApplicationAdapter _cadAdapter;
        private IArasCadClient _arasClient;
        private ArasLoginResult _loginResult;

        private string _statusMessage = "Sign in to Aras to start.";
        private PartSearchResult _selectedSearchResult;
        private CadSummary _currentCad;
        private string _selectedPartId;
        private string _selectedCadId;
        private string _lockToken;
        private string _lastDownloadedFilePath;
        private bool _isBusy;
        private bool _isLoginPanelVisible = true;
        private CadOperationContext _cadOperationContext;
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalCount;
        private string _searchKeyword;

        public MainViewModel()
            : this(new ArasClientOptions(), null, new WorkspaceService(new WorkspaceOptions()))
        {
        }

        public MainViewModel(ArasClientOptions options, ICadApplicationAdapter cadAdapter, WorkspaceService workspaceService)
        {
            _loginViewModel = new LoginViewModel(options);
            _cadAdapter = cadAdapter ?? new IronCadExternalAdapter(options?.IronCadExecutablePath);
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));

            LoginCommand = new RelayCommand(_ => ExecuteLoginAsync(), _ => !IsBusy);
            SearchPartsCommand = new RelayCommand(_ => ExecuteSearchAsync(1), _ => !IsBusy && IsConnected);
            NextPageCommand = new RelayCommand(_ => ExecuteSearchAsync(_currentPage + 1), _ => !IsBusy && HasNextPage);
            PreviousPageCommand = new RelayCommand(_ => ExecuteSearchAsync(_currentPage - 1), _ => !IsBusy && HasPreviousPage);
            SelectAndCreateCadCommand = new RelayCommand(_ => ExecuteSelectAndCreateCadAsync(), _ => !IsBusy && SelectedSearchResult?.Part != null);
            CheckoutCommand = new RelayCommand(_ => ExecuteCheckoutAsync(), _ => !IsBusy && CanCheckoutCurrentCad());
            OpenReadOnlyCommand = new RelayCommand(_ => ExecuteOpenReadOnlyAsync(), _ => !IsBusy && CanOpenReadOnlyCurrentCad());
            CheckInCommand = new RelayCommand(_ => ExecuteCheckInAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SelectedCadId) && !string.IsNullOrWhiteSpace(_lockToken));
            CancelCheckoutCommand = new RelayCommand(_ => ExecuteCancelCheckoutAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SelectedCadId) && !string.IsNullOrWhiteSpace(_lockToken));

            StartDetailedDesignCommand = new RelayCommand(
                _ => ExecuteWorkflowActionAsync(CadBusinessActionKind.StartDetailedDesign),
                _ => !IsBusy && CanExecuteAction(CadBusinessActionKind.StartDetailedDesign));
            SubmitForReviewCommand = new RelayCommand(
                _ => ExecuteWorkflowActionAsync(CadBusinessActionKind.SubmitForReview),
                _ => !IsBusy && CanExecuteAction(CadBusinessActionKind.SubmitForReview));
            ApproveCommand = new RelayCommand(
                _ => ExecuteWorkflowActionAsync(CadBusinessActionKind.Approve),
                _ => !IsBusy && CanExecuteAction(CadBusinessActionKind.Approve));
            RequestReworkCommand = new RelayCommand(
                _ => ExecuteWorkflowActionAsync(CadBusinessActionKind.RequestRework),
                _ => !IsBusy && CanExecuteAction(CadBusinessActionKind.RequestRework));

            RefreshWorkflowCommand = new RelayCommand(
                _ => ExecuteRefreshWorkflowAsync(),
                _ => !IsBusy && HasCurrentCad);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LoginViewModel LoginViewModel => _loginViewModel;

        public ICommand LoginCommand { get; }
        public ICommand SearchPartsCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand SelectAndCreateCadCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand OpenReadOnlyCommand { get; }
        public ICommand CheckInCommand { get; }
        public ICommand CancelCheckoutCommand { get; }
        public ICommand StartDetailedDesignCommand { get; }
        public ICommand SubmitForReviewCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RequestReworkCommand { get; }
        public ICommand RefreshWorkflowCommand { get; }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIdle));
                RefreshCanExecute();
            }
        }

        public bool IsIdle => !IsBusy;

        public bool IsConnected => _loginResult != null;

        public bool IsLoginPanelVisible
        {
            get => _isLoginPanelVisible;
            set
            {
                if (_isLoginPanelVisible == value) return;
                _isLoginPanelVisible = value;
                OnPropertyChanged();
            }
        }

        public string ConnectedUserText =>
            IsConnected ? $"Connected as {_loginResult.UserName}" : "Not connected";

        public CadOperationContext CurrentOperationContext
        {
            get => _cadOperationContext;
            private set
            {
                if (_cadOperationContext == value) return;
                _cadOperationContext = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWorkflowTask));
                OnPropertyChanged(nameof(WorkflowActivityName));
                OnPropertyChanged(nameof(WorkflowAssignee));
                OnPropertyChanged(nameof(WorkflowStatusText));
                OnPropertyChanged(nameof(AvailableActionButtons));
                OnPropertyChanged(nameof(HasStartDetailedDesignAction));
                OnPropertyChanged(nameof(HasSubmitForReviewAction));
            }
        }

        public bool HasWorkflowTask =>
            _cadOperationContext?.ActiveTask != null;

        public string WorkflowActivityName =>
            _cadOperationContext?.ActiveTask?.ActivityName ?? "No active task";

        public string WorkflowAssignee =>
            _cadOperationContext?.ActiveTask?.AssigneeName ?? "-";

        public string WorkflowStatusText
        {
            get
            {
                if (_cadOperationContext?.ActiveTask == null)
                    return "No active workflow task.";
                var paths = _cadOperationContext.ActiveTask.AvailablePaths;
                var incomplete = paths?.Any(p => !p.IsComplete) == true
                    ? paths.Where(p => !p.IsComplete).Count()
                    : 0;
                return $"Task: {_cadOperationContext.ActiveTask.ActivityName} ({incomplete} action(s) available)";
            }
        }

        public IReadOnlyList<CadBusinessAction> AvailableActionButtons =>
            _cadOperationContext?.AvailableActions ?? Array.Empty<CadBusinessAction>();

        public bool HasStartDetailedDesignAction =>
            AvailableActionButtons.Any(a => a.Kind == CadBusinessActionKind.StartDetailedDesign && a.IsAvailable);

        public bool HasSubmitForReviewAction =>
            AvailableActionButtons.Any(a => a.Kind == CadBusinessActionKind.SubmitForReview && a.IsAvailable);

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (_currentPage == value) return;
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                OnPropertyChanged(nameof(PageInfoText));
            }
        }

        public int TotalPages
        {
            get
            {
                if (_totalCount <= 0 || _pageSize <= 0) return 1;
                return (_totalCount + _pageSize - 1) / _pageSize;
            }
        }

        public bool HasPreviousPage => _currentPage > 1;

        public bool HasNextPage => _currentPage < TotalPages;

        public string PageInfoText
        {
            get
            {
                if (_totalCount <= 0) return "";
                return $"Page {_currentPage} / {TotalPages}";
            }
        }

        public PartSearchResult SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (_selectedSearchResult == value) return;
                _selectedSearchResult = value;
                SyncSelectionState();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPartSummary));
                OnPropertyChanged(nameof(CurrentCadSummary));
                OnPropertyChanged(nameof(CurrentCadLockStateText));
                OnPropertyChanged(nameof(CurrentCadFileStateText));
                OnPropertyChanged(nameof(CurrentCadStatusText));
                OnPropertyChanged(nameof(ActionHint));
                RefreshCanExecute();
            }
        }

        public string SelectedPartId
        {
            get => _selectedPartId;
            private set
            {
                if (_selectedPartId == value) return;
                _selectedPartId = value;
                OnPropertyChanged();
            }
        }

        public string SelectedCadId
        {
            get => _selectedCadId;
            private set
            {
                if (_selectedCadId == value) return;
                _selectedCadId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurrentCad));
                RefreshCanExecute();
            }
        }

        public bool HasCurrentCad => _currentCad != null && !string.IsNullOrWhiteSpace(_currentCad.Id);

        public string SelectedPartSummary => BuildPartSummary(SelectedSearchResult?.Part);

        public string CurrentCadSummary => BuildCadSummary(_currentCad);

        public string CurrentCadLockStateText
        {
            get
            {
                if (!HasCurrentCad)
                    return "No CAD";

                return _currentCad != null && _currentCad.IsLocked ? "Locked" : "Available";
            }
        }

        public string CurrentCadFileStateText
        {
            get
            {
                if (!HasCurrentCad)
                    return "No file";

                return _currentCad != null && _currentCad.HasNativeFile ? "Native file attached" : "Native file missing";
            }
        }

        public string CurrentCadStatusText
        {
            get
            {
                if (!HasCurrentCad)
                    return "No linked CAD selected yet.";

                if (!string.IsNullOrWhiteSpace(_lockToken))
                    return "Checked out by you in this session.";

                if (_currentCad.IsLocked)
                    return "This CAD is locked by another user.";

                if (!_currentCad.HasNativeFile)
                    return "This CAD exists, but it does not have a native file yet.";

                if (CadLifecyclePolicy.CanCheckout(_currentCad.State))
                    return "This CAD is editable in the current lifecycle state.";

                if (CadLifecyclePolicy.IsState(_currentCad.State, CadLifecyclePolicy.InReview))
                    return "This CAD is waiting for TNTKC review in Aras.";

                if (CadLifecyclePolicy.IsState(_currentCad.State, CadLifecyclePolicy.Released))
                    return "This CAD is released. Editing requires the approved Aras change process.";

                return CadLifecyclePolicy.GetCheckoutBlockedMessage(_currentCad.State);
            }
        }

        public string ActionHint
        {
            get
            {
                if (SelectedSearchResult?.Part == null)
                    return "Select a part from the results table.";

                if (!HasCurrentCad)
                    return "No linked CAD is selected yet. Use Select / Create CAD first.";

                if (!string.IsNullOrWhiteSpace(_lockToken))
                    return "CAD is checked out in this session. You can check in or cancel checkout.";

                if (_currentCad != null && _currentCad.IsLocked)
                    return "Another user currently holds the lock. Open read-only if you only need to inspect.";

                if (_currentCad != null
                    && !_currentCad.HasNativeFile
                    && CadLifecyclePolicy.CanCheckout(_currentCad.State))
                    return "No native file exists yet. Checkout will create the first local IronCAD file.";

                if (_currentCad != null && !_currentCad.HasNativeFile)
                    return "No native file exists, and the current Aras state does not allow checkout.";

                if (_currentCad != null && CadLifecyclePolicy.CanCheckout(_currentCad.State))
                    return "CAD is in an editable state. Use Checkout to edit or Open Read-Only to inspect.";

                if (_currentCad != null && CadLifecyclePolicy.CanStartDetailedDesign(_currentCad.State))
                    return "Initial drafting is complete. Use Start Detailed Design to move this CAD into 'Thiet ke chi tiet'.";

                if (_currentCad != null
                    && CadLifecyclePolicy.IsState(_currentCad.State, CadLifecyclePolicy.InReview))
                    return "TNTKC must approve or request rework in Aras. The plugin stays read-only.";

                if (_currentCad != null
                    && CadLifecyclePolicy.IsState(_currentCad.State, CadLifecyclePolicy.Released))
                    return "Released CAD is read-only here. Use the approved Aras change process for further work.";

                return CadLifecyclePolicy.GetCheckoutBlockedMessage(_currentCad?.State);
            }
        }

        private bool CanCheckoutCurrentCad()
        {
            if (string.IsNullOrWhiteSpace(SelectedCadId) || _currentCad == null)
                return false;

            if (!CadLifecyclePolicy.CanCheckout(_currentCad.State))
                return false;

            if (_currentCad.IsLocked || !string.IsNullOrWhiteSpace(_lockToken))
                return false;

            return true;
        }

        private bool CanOpenReadOnlyCurrentCad()
        {
            return !string.IsNullOrWhiteSpace(SelectedCadId)
                && _currentCad != null
                && _currentCad.HasNativeFile;
        }

        private async void ExecuteLoginAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StatusMessage = "Signing in...";

                _loginViewModel.IsConnected = false;
                var request = _loginViewModel.CreateRequest();
                (_arasClient as IDisposable)?.Dispose();
                _arasClient = null;

                _arasClient = new ArasCadClient(new ArasClientOptions
                {
                    BaseUri = new Uri(request.ServerUrl),
                    Database = request.Database
                });

                _loginResult = await _arasClient.LoginAsync(request, CancellationToken.None);
                _loginViewModel.IsConnected = true;
                IsLoginPanelVisible = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedUserText));
                StatusMessage = $"Connected as {_loginResult.UserName}. Search for a part to continue.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Login failed.";
                MessageBox.Show("Login failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteSearchAsync(int page)
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (page < 1) page = 1;

            try
            {
                IsBusy = true;
                StatusMessage = "Searching parts...";

                if (page == 1)
                {
                    _loginViewModel.ClearSearchResults();
                    ResetSelectionState();
                    _searchKeyword = _loginViewModel.SearchKeyword;
                }

                var skip = (page - 1) * _pageSize;
                var response = await _arasClient.SearchPartsAsync(new PartSearchRequest
                {
                    Keyword = _searchKeyword,
                    MaxResults = _pageSize,
                    Skip = skip
                }, CancellationToken.None);

                _totalCount = response.TotalCount;
                CurrentPage = page;

                _loginViewModel.SetSearchResults(response.Items, _searchKeyword);
                SelectedSearchResult = _loginViewModel.SelectedSearchResult;

                if (_totalCount > 0)
                {
                    StatusMessage = $"Found {_totalCount} part(s). Page {_currentPage}/{TotalPages}.";
                }
                else
                {
                    StatusMessage = "No parts found.";
                }

                RefreshCanExecute();
            }
            catch (Exception ex)
            {
                StatusMessage = "Search failed.";
                MessageBox.Show("Search failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteSelectAndCreateCadAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (SelectedSearchResult?.Part == null)
            {
                MessageBox.Show("Select a part first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Selecting or creating CAD...";

                var result = await _arasClient.CreateCadAsync(new CreateCadRequest
                {
                    PartId = SelectedSearchResult.Part.Id,
                    PartNumber = SelectedSearchResult.Part.PartNumber
                }, CancellationToken.None);

                ApplyCadSelection(result.Cad, clearSessionLock: true);
                StatusMessage = $"Selected CAD {result.Cad.CadNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Select / Create CAD failed.";
                MessageBox.Show("CAD creation failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteCheckoutAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (string.IsNullOrWhiteSpace(SelectedCadId))
            {
                MessageBox.Show("Select or create a CAD first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Checking out CAD...";

                var result = await _arasClient.CheckoutAsync(
                    new CadCheckoutRequest { PartId = SelectedPartId, CadId = SelectedCadId },
                    CancellationToken.None);

                _lockToken = result.LockToken;
                ApplyCadSelection(result.Cad, clearSessionLock: false);

                var targetDir = GetWorkspaceDirectory();
                if (result.Cad != null && result.Cad.HasNativeFile)
                {
                    _lastDownloadedFilePath = await _arasClient.DownloadNativeFileAsync(result.Cad.NativeFileId, targetDir, CancellationToken.None);
                    await _cadAdapter.OpenDocumentAsync(_lastDownloadedFilePath, CadOpenMode.Edit, CancellationToken.None);
                    StatusMessage = $"Checked out and opened {Path.GetFileName(_lastDownloadedFilePath)}.";
                }
                else
                {
                    var partNumber = SelectedSearchResult?.Part?.PartNumber ?? "new-part";
                    var fileName = $"{partNumber}.ics";
                    var filePath = Path.Combine(targetDir, fileName);
                    File.WriteAllBytes(filePath, Array.Empty<byte>());
                    _lastDownloadedFilePath = filePath;
                    await _cadAdapter.OpenDocumentAsync(filePath, CadOpenMode.Edit, CancellationToken.None);
                    StatusMessage = $"Checked out. Design and save to {fileName}.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Checkout failed.";
                MessageBox.Show("Checkout failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExecute();
            }
        }

        private async void ExecuteOpenReadOnlyAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (string.IsNullOrWhiteSpace(SelectedCadId))
            {
                MessageBox.Show("Select or create a CAD first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Opening CAD read-only...";

                var result = await _arasClient.OpenReadOnlyAsync(
                    new CadOpenReadOnlyRequest { PartId = SelectedPartId, CadId = SelectedCadId },
                    CancellationToken.None);

                ApplyCadSelection(result.Cad, clearSessionLock: false);

                if (result.Cad == null || !result.Cad.HasNativeFile)
                {
                    MessageBox.Show("The selected CAD does not have a native file yet.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "CAD has no native file.";
                    return;
                }

                var targetDir = GetWorkspaceDirectory();
                _lastDownloadedFilePath = await _arasClient.DownloadNativeFileAsync(result.Cad.NativeFileId, targetDir, CancellationToken.None);
                await _cadAdapter.OpenDocumentAsync(_lastDownloadedFilePath, CadOpenMode.ReadOnly, CancellationToken.None);

                StatusMessage = $"Opened {Path.GetFileName(_lastDownloadedFilePath)} in read-only mode.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Open read-only failed.";
                MessageBox.Show("Open read-only failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteCheckInAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (string.IsNullOrWhiteSpace(SelectedCadId) || string.IsNullOrWhiteSpace(_lockToken))
            {
                MessageBox.Show("Checkout the CAD first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Checking in CAD...";

                var filePath = ResolveCheckInFilePath();
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("No local file to check in. Save the file in IronCAD first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var uploadResult = await _arasClient.UploadFileAsync(new FileUploadRequest
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath)
                }, CancellationToken.None);

                var request = CadCheckinRequest.CreateNew();
                request.CadId = SelectedCadId;
                request.LockToken = _lockToken;
                request.UploadedFileId = uploadResult.UploadedFileId;
                request.LocalFilePath = filePath;
                request.Metadata = _cadAdapter.ReadMetadata();

                var result = await _arasClient.CheckinAsync(request, CancellationToken.None);
                _lockToken = null;
                ApplyCadSelection(result.Cad, clearSessionLock: false);

                StatusMessage = $"Check-in completed for {result.Cad.CadNumber}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Check-in failed.";
                MessageBox.Show("Check-in failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExecute();
            }
        }

        private async void ExecuteCancelCheckoutAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (string.IsNullOrWhiteSpace(SelectedCadId))
            {
                MessageBox.Show("No CAD selected.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Cancelling checkout...";

                await _arasClient.CancelCheckoutAsync(new CancelCheckoutRequest { CadId = SelectedCadId }, CancellationToken.None);
                _lockToken = null;

                if (_currentCad != null)
                {
                    _currentCad.IsLocked = false;
                    _currentCad.LockedBy = null;
                    OnPropertyChanged(nameof(CurrentCadSummary));
                    OnPropertyChanged(nameof(CurrentCadLockStateText));
                    OnPropertyChanged(nameof(CurrentCadStatusText));
                }

                if (SelectedSearchResult != null && SelectedSearchResult.IronCadPartCad != null)
                {
                    SelectedSearchResult.IronCadPartCad.IsLocked = false;
                    SelectedSearchResult.IronCadPartCad.LockedBy = null;
                }

                StatusMessage = "Checkout cancelled.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Cancel checkout failed.";
                MessageBox.Show("Cancel checkout failed: " + ex.Message, "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                RefreshCanExecute();
            }
        }

        private bool CanExecuteAction(CadBusinessActionKind kind)
        {
            return AvailableActionButtons?.Any(a => a.Kind == kind && a.IsAvailable) == true;
        }

        private async void ExecuteWorkflowActionAsync(CadBusinessActionKind kind)
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            var selectedCadId = SelectedCadId;
            if (string.IsNullOrWhiteSpace(selectedCadId))
                return;

            // Only approve/rework require an active workflow task
            if (kind != CadBusinessActionKind.SubmitForReview
                && kind != CadBusinessActionKind.StartDetailedDesign
                && _cadOperationContext?.ActiveTask == null)
                return;

            var action = _cadOperationContext?.AvailableActions?.FirstOrDefault(a => a.Kind == kind);
            if (action == null) return;

            var confirmMsg = kind switch
            {
                CadBusinessActionKind.StartDetailedDesign => "Move this CAD from 'Khoi tao' to 'Thiet ke chi tiet'?",
                CadBusinessActionKind.SubmitForReview => "Submit this CAD for review?",
                CadBusinessActionKind.Approve => "Approve this CAD?",
                CadBusinessActionKind.RequestRework => "Request rework on this CAD?",
                _ => $"Execute {kind}?"
            };

            var result = MessageBox.Show(confirmMsg, "Workflow Action", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"Executing {kind}...";

                var beforeContext = _cadOperationContext;
                if (beforeContext == null || !string.Equals(beforeContext.CadId, selectedCadId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Workflow context is stale. Refresh the selected CAD and try again.");
                }

                var request = new ExecuteCadBusinessActionRequest(
                    selectedCadId,
                    kind,
                    beforeContext?.ModifiedOn,
                    action.WorkflowTaskId,
                    action.WorkflowPathId,
                    comment: null);

                var updatedContext = await _arasClient.ExecuteCadBusinessActionAsync(
                    request, CancellationToken.None);

                if (IsCurrentCadSelection(selectedCadId))
                {
                    CurrentOperationContext = updatedContext;
                    StatusMessage = $"{kind} completed successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"{kind} failed.";
                MessageBox.Show($"{kind} failed: {ex.Message}", "Workflow Action",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Re-load context to reflect server state
                if (IsCurrentCadSelection(selectedCadId))
                    _ = LoadOperationContextAsync(selectedCadId, CancellationToken.None);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteRefreshWorkflowAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            if (string.IsNullOrWhiteSpace(SelectedCadId)) return;

            StatusMessage = "Refreshing workflow context...";
            try
            {
                IsBusy = true;
                var context = await _arasClient.GetCadOperationContextAsync(
                    SelectedCadId, CancellationToken.None);
                CurrentOperationContext = context;
                if (context?.ActiveTask != null)
                    StatusMessage = $"Workflow refreshed: {context.ActiveTask.ActivityName}.";
                else
                    StatusMessage = "Workflow refreshed: no active task.";
            }
            catch (Exception ex)
            {
                CurrentOperationContext = null;
                StatusMessage = "Workflow refresh failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string ResolveCheckInFilePath()
        {
            if (!string.IsNullOrWhiteSpace(_lastDownloadedFilePath) && File.Exists(_lastDownloadedFilePath))
            {
                return _lastDownloadedFilePath;
            }

            var docInfo = _cadAdapter.GetActiveDocumentInfo();
            return docInfo?.FullPath;
        }

        private string GetWorkspaceDirectory()
        {
            var filePath = _workspaceService.GetCadPartPath(SelectedPartId ?? "temp");
            _workspaceService.EnsureDirectoryForFile(filePath);
            return Path.GetDirectoryName(filePath);
        }

        private void SyncSelectionState()
        {
            SelectedPartId = SelectedSearchResult?.Part?.Id;
            _currentCad = SelectedSearchResult?.IronCadPartCad;
            SelectedCadId = _currentCad?.Id;
            OnPropertyChanged(nameof(HasCurrentCad));
        }

        private void ResetSelectionState()
        {
            _selectedSearchResult = null;
            _currentCad = null;
            SelectedPartId = null;
            SelectedCadId = null;
            _lockToken = null;
            _lastDownloadedFilePath = null;
            OnPropertyChanged(nameof(SelectedSearchResult));
            OnPropertyChanged(nameof(SelectedPartSummary));
            OnPropertyChanged(nameof(CurrentCadSummary));
            OnPropertyChanged(nameof(CurrentCadLockStateText));
            OnPropertyChanged(nameof(CurrentCadFileStateText));
            OnPropertyChanged(nameof(CurrentCadStatusText));
            OnPropertyChanged(nameof(ActionHint));
        }

        private void ApplyCadSelection(CadSummary cad, bool clearSessionLock)
        {
            _currentCad = cad;
            SelectedPartId = SelectedSearchResult?.Part?.Id;
            SelectedCadId = cad?.Id;

            if (clearSessionLock)
            {
                _lockToken = null;
            }

            if (SelectedSearchResult != null)
            {
                SelectedSearchResult.IronCadPartCad = cad;
                _loginViewModel.SelectedSearchResult = SelectedSearchResult;
            }

            OnPropertyChanged(nameof(CurrentCadSummary));
            OnPropertyChanged(nameof(CurrentCadLockStateText));
            OnPropertyChanged(nameof(CurrentCadFileStateText));
            OnPropertyChanged(nameof(CurrentCadStatusText));
            OnPropertyChanged(nameof(ActionHint));

            // Fire-and-forget workflow context load
            if (cad != null && !string.IsNullOrWhiteSpace(cad.Id) && _loginResult != null)
            {
                _ = LoadOperationContextAsync(cad.Id, CancellationToken.None);
            }
        }

        private async Task LoadOperationContextAsync(string cadId, CancellationToken ct)
        {
            try
            {
                var context = await _arasClient.GetCadOperationContextAsync(
                    cadId, ct);
                if (IsCurrentCadSelection(cadId))
                {
                    CurrentOperationContext = context;
                }
            }
            catch (Exception)
            {
                // Non-critical: workflow context loads asynchronously; ignore errors
                if (IsCurrentCadSelection(cadId))
                {
                    CurrentOperationContext = null;
                }
            }
        }

        private bool IsCurrentCadSelection(string cadId)
        {
            return !string.IsNullOrWhiteSpace(cadId)
                && string.Equals(SelectedCadId, cadId, StringComparison.OrdinalIgnoreCase);
        }

        private bool EnsureLoggedIn()
        {
            if (_arasClient == null || _loginResult == null)
            {
                MessageBox.Show("Please sign in first.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_loginResult.SessionToken))
            {
                MessageBox.Show("Session expired. Please sign in again.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static string BuildPartSummary(PartSummary part)
        {
            if (part == null)
                return "Select a part from the search results to see details here.";

            var builder = new StringBuilder();
            builder.AppendLine("Part");
            builder.AppendLine("  Number: " + Safe(part.PartNumber));
            builder.AppendLine("  Name: " + Safe(part.Name));
            builder.AppendLine("  Revision: " + Safe(part.Revision));
            builder.AppendLine("  State: " + Safe(part.State));
            builder.AppendLine("  Type: " + Safe(part.PartType));
            builder.Append("  Description: " + Safe(part.Description));
            return builder.ToString();
        }

        private static string BuildCadSummary(CadSummary cad)
        {
            if (cad == null)
                return "No CAD selected yet.";

            var builder = new StringBuilder();
            builder.AppendLine("Linked CAD");
            builder.AppendLine("  Number: " + Safe(cad.CadNumber));
            builder.AppendLine("  Revision: " + Safe(cad.Revision));
            builder.AppendLine("  State: " + Safe(cad.State));
            builder.AppendLine("  Classification: " + Safe(cad.Classification));
            builder.AppendLine("  Native file: " + (cad.HasNativeFile ? "Available" : "Missing"));
            builder.AppendLine("  Locked: " + (cad.IsLocked ? "Yes" : "No"));
            builder.Append("  Locked by: " + Safe(cad.LockedBy));
            return builder.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private void RefreshCanExecute()
        {
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SearchPartsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PreviousPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SelectAndCreateCadCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CheckoutCommand).RaiseCanExecuteChanged();
            ((RelayCommand)OpenReadOnlyCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CheckInCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelCheckoutCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StartDetailedDesignCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SubmitForReviewCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ApproveCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RequestReworkCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RefreshWorkflowCommand).RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
