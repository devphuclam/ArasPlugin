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
using Microsoft.Extensions.Logging;

namespace IdeaCadConnector.Desktop
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly LoginViewModel _loginViewModel;
        private readonly WorkspaceService _workspaceService;
        private readonly ICadApplicationAdapter _cadAdapter;
        private readonly ArasClientOptions _options;
        private CheckoutService _checkoutService;
        private IArasCadClient _arasClient;
        private ArasLoginResult _loginResult;
        internal static IPdmRepositoryClient SharedPdmClient { get; set; }
        internal static IArasCadClient SharedArasCadClient { get; set; }
        internal static string SharedUserName { get; set; }

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
        private int _workflowContextVersion;
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
            _options = options ?? new ArasClientOptions();
            _loginViewModel = new LoginViewModel(_options);
            _cadAdapter = cadAdapter ?? new IronCadExternalAdapter(options?.IronCadExecutablePath);
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));

            LoginCommand = new RelayCommand(_ => ExecuteLoginAsync(), _ => !IsBusy);
            LogoutCommand = new RelayCommand(_ => ExecuteLogoutAsync(), _ => !IsBusy && IsConnected);
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

            ViewPartDetailsCommand = new RelayCommand(
                _ => ExecuteViewPartDetailsAsync(),
                _ => SelectedSearchResult?.Part != null);

            ToggleFavoriteCommand = new RelayCommand(
                _ => ExecuteToggleFavoriteAsync(),
                _ => SelectedSearchResult?.Part != null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LoginViewModel LoginViewModel => _loginViewModel;

        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
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

        public ICommand ViewPartDetailsCommand { get; }

        public ICommand ToggleFavoriteCommand { get; }

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
                SharedArasCadClient = null;

                _arasClient = new HttpArasCadClient(new ArasClientOptions
                {
                    BaseUri = new Uri(request.ServerUrl),
                    Database = request.Database
                });
                SharedArasCadClient = _arasClient;

                _loginResult = await _arasClient.LoginAsync(request, CancellationToken.None);
                _loginViewModel.IsConnected = true;
                IsLoginPanelVisible = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedUserText));
                SharedUserName = _loginResult.UserName;
                StatusMessage = $"Connected as {_loginResult.UserName}. Search for a part to continue.";

                (SharedPdmClient as IDisposable)?.Dispose();
                var pdmClient = new HttpPdmRepositoryClient(new ArasClientOptions
                {
                    BaseUri = new Uri(request.ServerUrl),
                    Database = request.Database
                });
                pdmClient.SetSession(_loginResult.SessionToken, null, request.Database);
                SharedPdmClient = pdmClient;
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

        private async void ExecuteLogoutAsync()
        {
            try
            {
                IsBusy = true;
                _loginResult = null;
                _loginViewModel.IsConnected = false;
                _loginViewModel.SearchResults.Clear();
                _selectedSearchResult = null;
                _currentCad = null;
                _selectedPartId = null;
                _selectedCadId = null;
                _lockToken = null;
                _lastDownloadedFilePath = null;
                _cadOperationContext = null;
                CurrentOperationContext = null;
                _loginViewModel.SearchKeyword = null;
                _loginViewModel.ErrorMessage = null;
                (_arasClient as IDisposable)?.Dispose();
                _arasClient = null;
                (SharedPdmClient as IDisposable)?.Dispose();
                SharedPdmClient = null;
                (SharedArasCadClient as IDisposable)?.Dispose();
                SharedArasCadClient = null;
                SharedUserName = null;
                IsLoginPanelVisible = true;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedUserText));
                OnPropertyChanged(nameof(SelectedSearchResult));
                OnPropertyChanged(nameof(SelectedCadId));
                OnPropertyChanged(nameof(HasCurrentCad));
                StatusMessage = "Signed out. Sign in to start.";
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

                var targetDir = GetWorkspaceDirectory();
                var result = await GetCheckoutService().CheckoutAndDownloadAsync(
                    SelectedCadId,
                    targetDir,
                    CancellationToken.None);

                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Checkout failed.");
                }

                _lockToken = result.LockToken;
                _lastDownloadedFilePath = result.LocalFilePath;
                ApplyCadSelection(result.Cad, clearSessionLock: false);
                await _cadAdapter.OpenDocumentAsync(_lastDownloadedFilePath, CadOpenMode.Edit, CancellationToken.None);
                StatusMessage = $"Checked out and opened {Path.GetFileName(_lastDownloadedFilePath)}.";
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

                var targetDir = GetWorkspaceDirectory();
                var result = await GetCheckoutService().OpenReadOnlyAsync(
                    SelectedCadId,
                    targetDir,
                    CancellationToken.None);

                if (!result.Success || string.IsNullOrWhiteSpace(result.LocalFilePath))
                {
                    MessageBox.Show(result.ErrorMessage ?? "The selected CAD does not have a native file yet.", "IDEA PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = result.ErrorMessage ?? "CAD has no native file.";
                    return;
                }

                ApplyCadSelection(result.Cad, clearSessionLock: false);
                _lastDownloadedFilePath = result.LocalFilePath;
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

                var result = await GetCheckoutService().UploadAndCheckinAsync(
                    SelectedCadId,
                    _lockToken,
                    filePath,
                    _cadAdapter.ReadMetadata(),
                    CancellationToken.None);

                if (!result.Success)
                    throw new InvalidOperationException(result.ErrorMessage ?? "Check-in failed.");

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

                var success = await GetCheckoutService().CancelCheckoutAsync(SelectedCadId, CancellationToken.None);
                if (!success)
                    throw new InvalidOperationException("Cancel checkout failed.");
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

        private CheckoutService GetCheckoutService()
        {
            if (_checkoutService == null)
            {
                _checkoutService = new CheckoutService(_arasClient, _workspaceService);
            }

            return _checkoutService;
        }

        private bool CanExecuteAction(CadBusinessActionKind kind)
        {
            if (kind == CadBusinessActionKind.StartDetailedDesign)
            {
                return _currentCad != null
                    && CadLifecyclePolicy.CanStartDetailedDesign(_currentCad.State);
            }

            if (kind == CadBusinessActionKind.SubmitForReview)
            {
                return _currentCad != null
                    && CadLifecyclePolicy.CanSubmitForReview(_currentCad.State);
            }

            return AvailableActionButtons?.Any(a => a.Kind == kind && a.IsAvailable) == true;
        }

        private async void ExecuteWorkflowActionAsync(CadBusinessActionKind kind)
        {
            if (!EnsureLoggedIn() || IsBusy) return;
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

            string selectedCadId = null;
            try
            {
                IsBusy = true;
                StatusMessage = $"Executing {kind}...";

                if ((kind == CadBusinessActionKind.StartDetailedDesign
                    || kind == CadBusinessActionKind.SubmitForReview)
                    && SelectedSearchResult?.Part != null)
                {
                    var ensuredCad = await _arasClient.CreateCadAsync(new CreateCadRequest
                    {
                        PartId = SelectedSearchResult.Part.Id,
                        PartNumber = SelectedSearchResult.Part.PartNumber
                    }, CancellationToken.None);

                    if (ensuredCad?.Cad != null)
                    {
                        ApplyCadSelection(ensuredCad.Cad, clearSessionLock: false);
                    }
                }

                selectedCadId = SelectedCadId;
                if (string.IsNullOrWhiteSpace(selectedCadId))
                    throw new InvalidOperationException("No CAD is selected for this workflow action.");

                // Only approve/rework require an active workflow task
                if (kind != CadBusinessActionKind.SubmitForReview
                    && kind != CadBusinessActionKind.StartDetailedDesign
                    && _cadOperationContext?.ActiveTask == null)
                    return;

                var action = _cadOperationContext?.AvailableActions?.FirstOrDefault(a => a.Kind == kind);
                if (action == null
                    && (kind == CadBusinessActionKind.StartDetailedDesign
                        || kind == CadBusinessActionKind.SubmitForReview))
                {
                    action = new CadBusinessAction(
                        kind,
                        kind == CadBusinessActionKind.StartDetailedDesign ? "Start Detailed Design" : "Submit for Review",
                        true,
                        null,
                        false,
                        null,
                        null);
                }

                if (action == null)
                    throw new InvalidOperationException($"Action '{kind}' is not available for the selected CAD.");

                var beforeContext = _cadOperationContext;
                var requiresLiveTaskContext =
                    kind != CadBusinessActionKind.StartDetailedDesign
                    && kind != CadBusinessActionKind.SubmitForReview;

                if (requiresLiveTaskContext
                    && (beforeContext == null || !string.Equals(beforeContext.CadId, selectedCadId, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Workflow context is stale. Refresh the selected CAD and try again.");
                }

                CadOperationContext freshContext = null;
                try
                {
                    freshContext = await _arasClient.GetCadOperationContextAsync(
                        selectedCadId, CancellationToken.None);
                }
                catch
                {
                    throw;
                }

                if (!IsCurrentCadSelection(selectedCadId)
                    || freshContext == null
                    || !string.Equals(freshContext.CadId, selectedCadId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Workflow context changed while preparing the action. Refresh and try again.");
                }

                CadBusinessAction resolvedAction;
                if (kind == CadBusinessActionKind.StartDetailedDesign
                    || kind == CadBusinessActionKind.SubmitForReview)
                {
                    resolvedAction = action;
                }
                else
                {
                    resolvedAction = ResolveActionForExecution(
                        freshContext,
                        kind,
                        action.WorkflowTaskId,
                        action.WorkflowPathId);
                }

                var request = new ExecuteCadBusinessActionRequest(
                    selectedCadId,
                    kind,
                    freshContext.ModifiedOn,
                    resolvedAction.WorkflowTaskId,
                    resolvedAction.WorkflowPathId,
                    comment: null);

                var updatedContext = await _arasClient.ExecuteCadBusinessActionAsync(
                    request, CancellationToken.None);

                if (IsCurrentCadSelection(selectedCadId)
                    && updatedContext != null
                    && string.Equals(updatedContext.CadId, selectedCadId, StringComparison.OrdinalIgnoreCase))
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
                {
                    var reloadVersion = Interlocked.Increment(ref _workflowContextVersion);
                    CurrentOperationContext = null;
                    _ = LoadOperationContextAsync(selectedCadId, reloadVersion, CancellationToken.None);
                }
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
                var refreshVersion = Interlocked.Increment(ref _workflowContextVersion);
                CurrentOperationContext = null;
                var context = await _arasClient.GetCadOperationContextAsync(
                    SelectedCadId, CancellationToken.None);
                if (refreshVersion == _workflowContextVersion
                    && IsCurrentCadSelection(SelectedCadId)
                    && context != null
                    && string.Equals(context.CadId, SelectedCadId, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentOperationContext = context;
                }

                if (CurrentOperationContext?.ActiveTask != null)
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

        private async void ExecuteViewPartDetailsAsync()
        {
            if (SelectedSearchResult?.Part == null) return;

            var partId = SelectedSearchResult.Part.Id;
            var partNumber = SelectedSearchResult.Part.PartNumber;

            try
            {
                var baseUrl = _options.BaseUri.ToString().TrimEnd('/');
                var itemUrl = $"{baseUrl}/Client/../Item.aspx?itemtypeid=4F1AC04A2B484F3ABA4E20DB63808A88&id={partId}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = itemUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not open part details: {ex.Message}";
            }
        }

        private async void ExecuteToggleFavoriteAsync()
        {
            if (SelectedSearchResult?.Part == null) return;
            StatusMessage = "Favorites feature coming soon.";
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
            var loadVersion = Interlocked.Increment(ref _workflowContextVersion);
            CurrentOperationContext = null;
            OnPropertyChanged(nameof(HasCurrentCad));

            if (_currentCad != null
                && !string.IsNullOrWhiteSpace(_currentCad.Id)
                && _loginResult != null)
            {
                _ = LoadOperationContextAsync(_currentCad.Id, loadVersion, CancellationToken.None);
            }
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
            var previousCadId = SelectedCadId;
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
            if (!string.Equals(previousCadId, cad?.Id, StringComparison.OrdinalIgnoreCase))
            {
                CurrentOperationContext = null;
            }

            if (cad != null && !string.IsNullOrWhiteSpace(cad.Id) && _loginResult != null)
            {
                var loadVersion = Interlocked.Increment(ref _workflowContextVersion);
                _ = LoadOperationContextAsync(cad.Id, loadVersion, CancellationToken.None);
            }
        }

        private async Task LoadOperationContextAsync(string cadId, int loadVersion, CancellationToken ct)
        {
            try
            {
                var context = await _arasClient.GetCadOperationContextAsync(
                    cadId, ct);

                if (loadVersion == _workflowContextVersion
                    && IsCurrentCadSelection(cadId)
                    && context != null
                    && string.Equals(context.CadId, cadId, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentOperationContext = context;
                }
            }
            catch (Exception)
            {
                // Non-critical: workflow context loads asynchronously; ignore errors
                if (loadVersion == _workflowContextVersion
                    && IsCurrentCadSelection(cadId))
                {
                    CurrentOperationContext = null;
                }
            }
        }

        private static CadBusinessAction ResolveActionForExecution(
            CadOperationContext context,
            CadBusinessActionKind kind,
            string workflowTaskId,
            string workflowPathId)
        {
            var candidates = (context?.AvailableActions ?? Array.Empty<CadBusinessAction>())
                .Where(a => a != null && a.IsAvailable && a.Kind == kind)
                .ToList();

            if (candidates.Count == 0)
                throw new InvalidOperationException($"Action '{kind}' is not available after refreshing workflow context.");

            if (!string.IsNullOrWhiteSpace(workflowTaskId) || !string.IsNullOrWhiteSpace(workflowPathId))
            {
                var exact = candidates.FirstOrDefault(a =>
                    (string.IsNullOrWhiteSpace(workflowTaskId)
                        || string.Equals(a.WorkflowTaskId, workflowTaskId, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrWhiteSpace(workflowPathId)
                        || string.Equals(a.WorkflowPathId, workflowPathId, StringComparison.OrdinalIgnoreCase)));

                if (exact != null)
                    return exact;
            }

            if (candidates.Count == 1)
                return candidates[0];

            throw new InvalidOperationException(
                $"Multiple '{kind}' workflow actions are currently available. Refresh and choose again from the live context.");
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
            ((RelayCommand)LogoutCommand).RaiseCanExecuteChanged();
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
