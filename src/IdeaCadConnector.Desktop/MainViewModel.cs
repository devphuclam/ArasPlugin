using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop.Services;
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
        private readonly IRevisionService _revisionService;
        private IArasCadClient _arasClient;
        private ArasLoginResult _loginResult;
        internal static IPdmRepositoryClient SharedPdmClient
        {
            get => AppSessionContext.Current.PdmClient;
            set => AppSessionContext.Current.PdmClient = value;
        }

        internal static IArasCadClient SharedArasCadClient
        {
            get => AppSessionContext.Current.ArasCadClient;
            set => AppSessionContext.Current.ArasCadClient = value;
        }

        internal static string SharedUserName
        {
            get => AppSessionContext.Current.CurrentUserName;
            set => AppSessionContext.Current.CurrentUserName = value;
        }

        internal static IPartLibraryClient SharedPartLibraryClient
        {
            get => AppSessionContext.Current.PartLibraryClient;
            set => AppSessionContext.Current.PartLibraryClient = value;
        }

        private string _statusMessage = LocalizationSource.Instance[TranslationKeys.StatusSignInToStart];
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
        private PdmRevisePreconditionResult _revisionPreconditions;
        private string _searchRevisionReadinessText;

        public MainViewModel()
            : this(ArasClientOptionsFactory.Current ?? new ArasClientOptions(), null, new WorkspaceService(new WorkspaceOptions()))
        {
        }

        public MainViewModel(ArasClientOptions options, ICadApplicationAdapter cadAdapter, WorkspaceService workspaceService)
        {
            _options = options ?? new ArasClientOptions();
            _loginViewModel = new LoginViewModel(_options);
            _cadAdapter = cadAdapter ?? new IronCadExternalAdapter(options?.IronCadExecutablePath);
            _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
            _revisionService = new GuidanceRevisionService();
            AppSessionContext.Current.IronCadExecutablePath = _options.IronCadExecutablePath;

            _selectedLanguage = SettingsService.LoadLanguage() ?? "en-US";

            LoginCommand = new RelayCommand(_ => ExecuteLoginAsync(), _ => !IsBusy);
            LogoutCommand = new RelayCommand(_ => ExecuteLogoutAsync(), _ => !IsBusy && IsConnected);
            SearchPartsCommand = new RelayCommand(_ => ExecuteSearchAsync(1), _ => !IsBusy && IsConnected);
            NextPageCommand = new RelayCommand(_ => ExecuteSearchAsync(_currentPage + 1), _ => !IsBusy && HasNextPage);
            PreviousPageCommand = new RelayCommand(_ => ExecuteSearchAsync(_currentPage - 1), _ => !IsBusy && HasPreviousPage);
            SelectAndCreateCadCommand = new RelayCommand(_ => ExecuteSelectAndCreateCadAsync(), _ => !IsBusy && SelectedSearchResult?.Part != null && !IsSelectedSearchAssemblyCandidate());
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

            AddSelectedPartToLibraryCommand = new RelayCommand(
                _ => ExecuteAddSelectedPartToLibraryAsync(),
                _ => !IsBusy && IsConnected && SelectedSearchResult?.Part != null && SharedPartLibraryClient != null);

            StartNewRevisionCommand = new RelayCommand(
                _ => { _ = ExecuteStartNewRevisionAsync(); },
                _ => !IsBusy && CanStartNewRevision);
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

        public ICommand AddSelectedPartToLibraryCommand { get; }

        public ICommand StartNewRevisionCommand { get; }

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

        public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new List<LanguageOption>
        {
            new LanguageOption { DisplayName = Loc(TranslationKeys.LanguageEnglish), CultureName = "en-US" },
            new LanguageOption { DisplayName = Loc(TranslationKeys.LanguageVietnamese), CultureName = "vi-VN" },
            new LanguageOption { DisplayName = Loc(TranslationKeys.LanguageJapanese), CultureName = "ja-JP" },
        };

        private string _selectedLanguage;

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage == value) return;
                _selectedLanguage = value;
                OnPropertyChanged();
                ApplyLanguage(value);
            }
        }

        private void ApplyLanguage(string cultureName)
        {
            try
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                SettingsService.SaveLanguage(cultureName);
                LocalizationSource.Instance.RaiseAllChanged();
                OnPropertyChanged(nameof(ConnectedUserText));
                OnPropertyChanged(nameof(WorkflowActivityName));
                OnPropertyChanged(nameof(WorkflowAssignee));
                OnPropertyChanged(nameof(WorkflowStatusText));
                OnPropertyChanged(nameof(CurrentCadLockStateText));
                OnPropertyChanged(nameof(CurrentCadFileStateText));
                OnPropertyChanged(nameof(CurrentCadStatusText));
                OnPropertyChanged(nameof(ActionHint));
                OnPropertyChanged(nameof(PageInfoText));
                OnPropertyChanged(nameof(CurrentCadSummary));
                OnPropertyChanged(nameof(SelectedPartSummary));
                OnPropertyChanged(nameof(HasSearchRevisionEntryPoint));
                OnPropertyChanged(nameof(SearchRevisionHint));
                OnPropertyChanged(nameof(HasSearchRevisionHint));
                OnPropertyChanged(nameof(CanStartNewRevision));
                OnPropertyChanged(nameof(SelectedSearchResult));
                OnPropertyChanged(nameof(AvailableActionButtons));
                OnPropertyChanged(nameof(HasWorkflowTask));
                OnPropertyChanged(nameof(HasStartDetailedDesignAction));
                OnPropertyChanged(nameof(HasSubmitForReviewAction));
                OnPropertyChanged(nameof(HasApproveAction));
                OnPropertyChanged(nameof(HasRequestReworkAction));
                OnPropertyChanged(nameof(HasAnyWorkflowAction));
                OnPropertyChanged(nameof(HasOpenReadOnlyAction));
                OnPropertyChanged(nameof(HasCheckoutAction));
                OnPropertyChanged(nameof(HasCheckInAction));
                OnPropertyChanged(nameof(HasCancelCheckoutAction));
                OnPropertyChanged(nameof(StatusMessage));
                OnPropertyChanged(nameof(HasAddSelectedPartToLibraryAction));
            }
            catch
            {
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
            IsConnected ? string.Format(Loc(TranslationKeys.ConnectedAs), _loginResult.UserName) : Loc(TranslationKeys.NotConnectedShort);

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
                OnPropertyChanged(nameof(HasApproveAction));
                OnPropertyChanged(nameof(HasRequestReworkAction));
                OnPropertyChanged(nameof(HasAnyWorkflowAction));
            }
        }

        public bool HasWorkflowTask =>
            _cadOperationContext?.ActiveTask != null;

        public string WorkflowActivityName =>
            _cadOperationContext?.ActiveTask?.ActivityName ?? Loc(TranslationKeys.NoActiveTask);

        public string WorkflowAssignee =>
            _cadOperationContext?.ActiveTask?.AssigneeName ?? "-";

        public string WorkflowStatusText
        {
            get
            {
                if (_cadOperationContext?.ActiveTask == null)
                    return LifecycleDisplayText.GetWorkflowIdleText(_currentCad?.State);
                var paths = _cadOperationContext.ActiveTask.AvailablePaths;
                var incomplete = paths?.Any(p => !p.IsComplete) == true
                    ? paths.Where(p => !p.IsComplete).Count()
                    : 0;
                return string.Format(Loc(TranslationKeys.TaskActionsAvailable), _cadOperationContext.ActiveTask.ActivityName, incomplete);
            }
        }

        public IReadOnlyList<CadBusinessAction> AvailableActionButtons =>
            _cadOperationContext?.AvailableActions ?? Array.Empty<CadBusinessAction>();

        public bool HasStartDetailedDesignAction =>
            AvailableActionButtons.Any(a => a.Kind == CadBusinessActionKind.StartDetailedDesign && a.IsAvailable);

        public bool HasSubmitForReviewAction =>
            HasWorkflowAction(CadBusinessActionKind.SubmitForReview);

        public bool HasApproveAction =>
            HasWorkflowAction(CadBusinessActionKind.Approve);

        public bool HasRequestReworkAction =>
            HasWorkflowAction(CadBusinessActionKind.RequestRework);

        public bool HasAnyWorkflowAction =>
            HasStartDetailedDesignAction
            || HasSubmitForReviewAction
            || HasApproveAction
            || HasRequestReworkAction;

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
                return string.Format(Loc(TranslationKeys.PageXOfY), _currentPage, TotalPages);
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
                OnPropertyChanged(nameof(HasSearchRevisionEntryPoint));
                OnPropertyChanged(nameof(SearchRevisionHint));
                OnPropertyChanged(nameof(HasSearchRevisionHint));
                OnPropertyChanged(nameof(HasOpenReadOnlyAction));
                OnPropertyChanged(nameof(HasCheckoutAction));
                OnPropertyChanged(nameof(HasCheckInAction));
                OnPropertyChanged(nameof(HasCancelCheckoutAction));
                OnPropertyChanged(nameof(HasAddSelectedPartToLibraryAction));
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
                    return Loc(TranslationKeys.NoCadShort);

                return _currentCad != null && _currentCad.IsLocked ? Loc(TranslationKeys.LockedShort) : Loc(TranslationKeys.AvailableShort);
            }
        }

        public string CurrentCadFileStateText
        {
            get
            {
                if (!HasCurrentCad)
                    return Loc(TranslationKeys.NoFileShort);

                return _currentCad != null && _currentCad.HasNativeFile ? Loc(TranslationKeys.NativeFileAttached) : Loc(TranslationKeys.NativeFileMissing);
            }
        }

        public string CurrentCadStatusText
        {
            get
            {
                if (!HasCurrentCad)
                    return Loc(TranslationKeys.NoCadSelectedYet);

                if (!string.IsNullOrWhiteSpace(_lockToken))
                {
                    if (!CadLifecyclePolicy.CanCheckout(_currentCad.State))
                        return LifecycleDisplayText.GetStaleSessionMessage(_currentCad.State);
                    return Loc(TranslationKeys.CheckedOutByYou);
                }

                if (_currentCad.IsLocked)
                    return Loc(TranslationKeys.CadLockedByOther);

                if (!_currentCad.HasNativeFile)
                    return Loc(TranslationKeys.CadNoNativeFile);

                return LifecycleDisplayText.GetStateSummary(_currentCad.State);
            }
        }

        public string ActionHint
        {
            get
            {
                if (SelectedSearchResult?.Part == null)
                    return Loc(TranslationKeys.ActionHintSelectPart);

                if (IsSelectedSearchAssemblyCandidate())
                    return CadNodeHelper.GetAssemblySearchCadHint();

                if (!HasCurrentCad)
                    return Loc(TranslationKeys.ActionHintNoCad);

                if (!string.IsNullOrWhiteSpace(_lockToken))
                {
                    if (!CadLifecyclePolicy.CanCheckout(_currentCad.State))
                        return Loc(TranslationKeys.ActionHintStateChanged);
                    return Loc(TranslationKeys.ActionHintCheckedOut);
                }

                if (_currentCad != null && _currentCad.IsLocked)
                    return Loc(TranslationKeys.ActionHintOtherLocked);

                return LifecycleDisplayText.GetActionGuidance(_currentCad?.State);
            }
        }

        public bool HasSearchRevisionEntryPoint =>
            GuidanceRevisionService.ShouldShowRevisionEntryPoint(_currentCad?.Id, SearchRevisionHint);

        public bool CanStartNewRevision => _revisionPreconditions?.CanRevise ?? false;

        public string SearchRevisionHint
        {
            get => _searchRevisionReadinessText ?? string.Empty;
            private set
            {
                if (_searchRevisionReadinessText != value)
                {
                    _searchRevisionReadinessText = value;
                    OnPropertyChanged(nameof(SearchRevisionHint));
                    OnPropertyChanged(nameof(HasSearchRevisionHint));
                    OnPropertyChanged(nameof(HasSearchRevisionEntryPoint));
                }
            }
        }

        public bool HasSearchRevisionHint => !string.IsNullOrWhiteSpace(SearchRevisionHint);

        public bool HasAddSelectedPartToLibraryAction =>
            IsConnected && SelectedSearchResult?.Part != null;

        public bool HasOpenReadOnlyAction =>
            !string.IsNullOrWhiteSpace(SelectedCadId)
            && _currentCad != null
            && _currentCad.HasNativeFile;

        public bool HasCheckoutAction =>
            !string.IsNullOrWhiteSpace(SelectedCadId)
            && _currentCad != null
            && CadLifecyclePolicy.CanCheckout(_currentCad.State)
            && string.IsNullOrWhiteSpace(_lockToken)
            && !_currentCad.IsLocked;

        public bool HasCheckInAction =>
            !string.IsNullOrWhiteSpace(SelectedCadId)
            && !string.IsNullOrWhiteSpace(_lockToken);

        public bool HasCancelCheckoutAction =>
            !string.IsNullOrWhiteSpace(SelectedCadId)
            && !string.IsNullOrWhiteSpace(_lockToken);

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
                StatusMessage = Loc(TranslationKeys.StatusSigningIn);

                _loginViewModel.IsConnected = false;
                var request = _loginViewModel.CreateRequest();
                (_arasClient as IDisposable)?.Dispose();
                _arasClient = null;
                SharedArasCadClient = null;

                var loginOptions = ArasClientOptionsFactory.Current.WithLoginOverrides(request.ServerUrl, request.Database);
                _arasClient = new HttpArasCadClient(loginOptions);
                SharedArasCadClient = _arasClient;

                _loginResult = await _arasClient.LoginAsync(request, CancellationToken.None);
                _loginViewModel.IsConnected = true;
                IsLoginPanelVisible = false;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedUserText));
                AppSessionContext.Current.ArasServerUrl = request.ServerUrl;
                AppSessionContext.Current.ArasDatabase = request.Database;
                SharedUserName = _loginResult.UserName;
                StatusMessage = string.Format(Loc(TranslationKeys.StatusConnected), _loginResult.UserName);

                (SharedPdmClient as IDisposable)?.Dispose();
                var pdmClient = new HttpPdmRepositoryClient(loginOptions);
                pdmClient.SetSession(_loginResult.SessionToken, null, request.Database);
                SharedPdmClient = pdmClient;
                (SharedPartLibraryClient as IDisposable)?.Dispose();
                var partLibraryClient = new HttpPartLibraryClient(loginOptions);
                partLibraryClient.SetSession(_loginResult.SessionToken, null, request.Database);
                SharedPartLibraryClient = partLibraryClient;
                AppSessionContext.Current.NotifyLibraryDataChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusLoginFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgLoginFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                (SharedPartLibraryClient as IDisposable)?.Dispose();
                SharedPartLibraryClient = null;
                AppSessionContext.Current.ArasServerUrl = null;
                AppSessionContext.Current.ArasDatabase = null;
                AppSessionContext.Current.NotifyLibraryDataChanged();
                AppSessionContext.Current.CurrentPdmProjectsViewModel = null;
                AppSessionContext.Current.PendingLibraryFocusLibraryId = null;
                AppSessionContext.Current.PendingLibraryFocusEntryId = null;
                IsLoginPanelVisible = true;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectedUserText));
                OnPropertyChanged(nameof(SelectedSearchResult));
                OnPropertyChanged(nameof(SelectedCadId));
                OnPropertyChanged(nameof(HasCurrentCad));
                OnPropertyChanged(nameof(HasAddSelectedPartToLibraryAction));
                StatusMessage = Loc(TranslationKeys.StatusSignedOut);
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
                StatusMessage = Loc(TranslationKeys.StatusSearching);

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
                    StatusMessage = string.Format(Loc(TranslationKeys.StatusFoundResults), _totalCount, _currentPage, TotalPages);
                }
                else
                {
                    StatusMessage = Loc(TranslationKeys.StatusNoResults);
                }

                RefreshCanExecute();
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusSearchFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgSearchFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Loc(TranslationKeys.MsgSelectPartFirst), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsSelectedSearchAssemblyCandidate())
            {
                MessageBox.Show(CadNodeHelper.GetAssemblySearchCadHint(), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = Loc(TranslationKeys.StatusSelectingCad);

                var result = await _arasClient.CreateCadAsync(new CreateCadRequest
                {
                    PartId = SelectedSearchResult.Part.Id,
                    PartNumber = SelectedSearchResult.Part.PartNumber,
                    PartClassification = SelectedSearchResult.Part.PartType
                }, CancellationToken.None);

                ApplyCadSelection(result.Cad, clearSessionLock: true);
                StatusMessage = string.Format(Loc(TranslationKeys.StatusSelectedCad), result.Cad.CadNumber);
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusCadSelectionFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgCadCreationFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Loc(TranslationKeys.MsgSelectCadFirst), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = Loc(TranslationKeys.StatusCheckingOut);

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
                StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckedOut), Path.GetFileName(_lastDownloadedFilePath));
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusCheckoutFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgCheckoutFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Loc(TranslationKeys.MsgSelectCadFirst), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = Loc(TranslationKeys.StatusOpeningReadOnly);

                var targetDir = GetWorkspaceDirectory();
                var result = await GetCheckoutService().OpenReadOnlyAsync(
                    SelectedCadId,
                    targetDir,
                    CancellationToken.None);

                if (!result.Success || string.IsNullOrWhiteSpace(result.LocalFilePath))
                {
                    MessageBox.Show(result.ErrorMessage ?? Loc(TranslationKeys.MsgCadNoNativeFile), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = result.ErrorMessage ?? Loc(TranslationKeys.ErrorNoCadSelected);
                    return;
                }

                ApplyCadSelection(result.Cad, clearSessionLock: false);
                _lastDownloadedFilePath = result.LocalFilePath;
                await _cadAdapter.OpenDocumentAsync(_lastDownloadedFilePath, CadOpenMode.ReadOnly, CancellationToken.None);

                StatusMessage = string.Format(Loc(TranslationKeys.StatusOpenedReadOnly), Path.GetFileName(_lastDownloadedFilePath));
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusOpenReadOnlyFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgOpenReadOnlyFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Loc(TranslationKeys.MsgCheckoutCadFirst), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = Loc(TranslationKeys.StatusCheckingIn);

                var filePath = ResolveCheckInFilePath();
                if (!File.Exists(filePath))
                {
                    MessageBox.Show(Loc(TranslationKeys.MsgNoLocalFileCheckin), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
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

                StatusMessage = string.Format(Loc(TranslationKeys.StatusCheckedIn), result.Cad.CadNumber);
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusCheckinFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgCheckinFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Loc(TranslationKeys.MsgNoCadSelected), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = Loc(TranslationKeys.StatusCancellingCheckout);

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

                _ = RefreshRevisionPreconditionsAsync();
                StatusMessage = Loc(TranslationKeys.StatusCheckoutCancelled);
            }
            catch (Exception ex)
            {
                StatusMessage = Loc(TranslationKeys.StatusCancelFailed);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgCancelCheckoutFailed), ex.Message), Ttl, MessageBoxButton.OK, MessageBoxImage.Error);
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
            return _currentCad != null
                && CadLifecyclePolicy.CanExecuteBusinessAction(kind, _currentCad.State);
        }

        private bool HasWorkflowAction(CadBusinessActionKind kind)
        {
            return _currentCad != null
                && CadLifecyclePolicy.ShouldShowBusinessAction(kind, _currentCad.State);
        }

        private async void ExecuteWorkflowActionAsync(CadBusinessActionKind kind)
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            var confirmMsg = kind switch
            {
                CadBusinessActionKind.StartDetailedDesign => Loc(TranslationKeys.ConfirmStartDetailedDesign),
                CadBusinessActionKind.SubmitForReview => Loc(TranslationKeys.ConfirmSubmitForReview),
                CadBusinessActionKind.Approve => Loc(TranslationKeys.ConfirmApprove),
                CadBusinessActionKind.RequestRework => Loc(TranslationKeys.ConfirmRequestRework),
                _ => string.Format(Loc(TranslationKeys.ConfirmExecuteAction), kind)
            };

            var result = MessageBox.Show(confirmMsg, Loc(TranslationKeys.WorkflowActionTitle), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            string selectedCadId = null;
            try
            {
                IsBusy = true;
                StatusMessage = string.Format(Loc(TranslationKeys.StatusExecutingAction), kind);

                if ((kind == CadBusinessActionKind.StartDetailedDesign
                    || kind == CadBusinessActionKind.SubmitForReview)
                    && SelectedSearchResult?.Part != null)
                {
                    if (IsSelectedSearchAssemblyCandidate())
                    {
                        MessageBox.Show(CadNodeHelper.GetAssemblySearchCadHint(), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var ensuredCad = await _arasClient.CreateCadAsync(new CreateCadRequest
                    {
                        PartId = SelectedSearchResult.Part.Id,
                        PartNumber = SelectedSearchResult.Part.PartNumber,
                        PartClassification = SelectedSearchResult.Part.PartType
                    }, CancellationToken.None);

                    if (ensuredCad?.Cad != null)
                    {
                        ApplyCadSelection(ensuredCad.Cad, clearSessionLock: false);
                    }
                }

                selectedCadId = SelectedCadId;
                if (string.IsNullOrWhiteSpace(selectedCadId))
                    throw new InvalidOperationException("No CAD is selected for this workflow action.");

                var action = _cadOperationContext?.AvailableActions?.FirstOrDefault(a => a.Kind == kind);
                action ??= new CadBusinessAction(kind, kind.ToString(), true, null, false, null, null);

                var beforeContext = _cadOperationContext;
                var requiresLiveTaskContext = false;

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
                resolvedAction = action;

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
                    StatusMessage = string.Format(Loc(TranslationKeys.StatusActionCompleted), kind);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusActionFailed), kind);
                MessageBox.Show(string.Format(Loc(TranslationKeys.MsgWorkflowActionFailed), kind, ex.Message), Loc(TranslationKeys.WorkflowActionTitle),
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

            StatusMessage = Loc(TranslationKeys.StatusRefreshingWorkflow);
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
                    StatusMessage = string.Format(Loc(TranslationKeys.StatusWorkflowRefreshed), context.ActiveTask.ActivityName);
                else
                    StatusMessage = Loc(TranslationKeys.StatusWorkflowNoActive);
            }
            catch (Exception ex)
            {
                CurrentOperationContext = null;
                StatusMessage = Loc(TranslationKeys.StatusWorkflowRefreshFailed) + " " + ex.Message;
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
                StatusMessage = Loc(TranslationKeys.StatusSearchFailed) + " " + ex.Message;
            }
        }

        private async void ExecuteToggleFavoriteAsync()
        {
            if (SelectedSearchResult?.Part == null) return;
            StatusMessage = Loc(TranslationKeys.FavoritesComingSoon);
        }

        private async void ExecuteAddSelectedPartToLibraryAsync()
        {
            if (!EnsureLoggedIn() || IsBusy) return;
            var selectedPart = SelectedSearchResult?.Part;
            if (selectedPart == null)
            {
                StatusMessage = Loc(TranslationKeys.StatusSelectPartFirst);
                return;
            }

            var workspace = AppSessionContext.Current.CurrentPdmProjectsViewModel;
            var sourceProject = workspace?.SelectedRepository ?? workspace?.RepositoryCodeForDisplay;
            var sourceCommit = workspace?.LatestCommitSummary;

            var workflowResult = await SaveToLibraryWorkflow.ExecuteAsync(
                new PartLibrarySaveSeed
                {
                    PartId = selectedPart.Id,
                    PartNumber = selectedPart.PartNumber,
                    PartName = selectedPart.Name,
                    SourceProject = sourceProject,
                    SourceCommit = sourceCommit
                },
                SharedPartLibraryClient).ConfigureAwait(true);

            if (!workflowResult.Submitted)
                return;

            if (workflowResult.AddResult?.Success == true)
            {
                AppSessionContext.Current.PendingLibraryFocusLibraryId = workflowResult.LibraryId;
                AppSessionContext.Current.PendingLibraryFocusEntryId = workflowResult.AddResult.EntryId;
                AppSessionContext.Current.NotifyLibraryDataChanged();

                if (workflowResult.AddResult.AlreadyExists)
                {
                    StatusMessage = string.Format(
                        Loc(TranslationKeys.LibraryStatusPartAlreadyInLibrary),
                        workflowResult.AddResult.EntryId ?? "-");
                    AppSessionContext.Current.RequestLibraryWorkspace();
                }
                else
                {
                    StatusMessage = string.Format(
                        Loc(TranslationKeys.LibraryStatusPartSavedToLibrary),
                        workflowResult.AddResult.EntryId ?? "-");
                }

                return;
            }

            StatusMessage = string.Format(
                Loc(TranslationKeys.LibraryStatusPartSaveFailed),
                workflowResult.AddResult?.ErrorMessage ?? workflowResult.ErrorMessage ?? Loc(TranslationKeys.UnknownError));
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

            _ = RefreshRevisionPreconditionsAsync();
        }

        private async Task RefreshRevisionPreconditionsAsync()
        {
            _revisionPreconditions = await _revisionService.CheckPreconditionsAsync(
                _currentCad?.State,
                _currentCad?.Id,
                SelectedPartId,
                _lockToken,
                CancellationToken.None);

            SearchRevisionHint = GuidanceRevisionService.BuildReadinessText(
                _revisionPreconditions,
                Loc(TranslationKeys.ReadyForRevision),
                Loc(TranslationKeys.RevisionRequiresReleased));

            OnPropertyChanged(nameof(CanStartNewRevision));
            ((RelayCommand)StartNewRevisionCommand).RaiseCanExecuteChanged();
        }

        private void ResetSelectionState()
        {
            _selectedSearchResult = null;
            _currentCad = null;
            SelectedPartId = null;
            SelectedCadId = null;
            _lockToken = null;
            _lastDownloadedFilePath = null;
            _revisionPreconditions = null;
            OnPropertyChanged(nameof(SelectedSearchResult));
            OnPropertyChanged(nameof(SelectedPartSummary));
            OnPropertyChanged(nameof(CurrentCadSummary));
            OnPropertyChanged(nameof(CurrentCadLockStateText));
            OnPropertyChanged(nameof(CurrentCadFileStateText));
            OnPropertyChanged(nameof(CurrentCadStatusText));
            OnPropertyChanged(nameof(ActionHint));
            OnPropertyChanged(nameof(HasSearchRevisionEntryPoint));
            OnPropertyChanged(nameof(HasOpenReadOnlyAction));
            OnPropertyChanged(nameof(HasCheckoutAction));
            OnPropertyChanged(nameof(HasCheckInAction));
            OnPropertyChanged(nameof(HasCancelCheckoutAction));
            OnPropertyChanged(nameof(HasAddSelectedPartToLibraryAction));
            OnPropertyChanged(nameof(CanStartNewRevision));
            SearchRevisionHint = string.Empty;
            ((RelayCommand)StartNewRevisionCommand).RaiseCanExecuteChanged();
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
            OnPropertyChanged(nameof(HasSearchRevisionEntryPoint));
            OnPropertyChanged(nameof(HasOpenReadOnlyAction));
            OnPropertyChanged(nameof(HasCheckoutAction));
            OnPropertyChanged(nameof(HasCheckInAction));
            OnPropertyChanged(nameof(HasCancelCheckoutAction));

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

            _ = RefreshRevisionPreconditionsAsync();
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
                MessageBox.Show(Loc(TranslationKeys.ErrorSignInFirst), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_loginResult.SessionToken))
            {
                MessageBox.Show(Loc(TranslationKeys.ErrorSessionExpired), Ttl, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static string BuildPartSummary(PartSummary part)
        {
            if (part == null)
                return Loc(TranslationKeys.SummarySelectPartFirst);

            var builder = new StringBuilder();
            builder.AppendLine(Loc(TranslationKeys.SummaryPartHeader));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryPartNumber), Safe(part.PartNumber)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryPartName), Safe(part.Name)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryPartRevision), Safe(part.Revision)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryPartState), Safe(part.State)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryPartType), Safe(part.PartType)));
            builder.Append(string.Format(Loc(TranslationKeys.SummaryPartDescription), Safe(part.Description)));
            return builder.ToString();
        }

        private static string BuildCadSummary(CadSummary cad)
        {
            if (cad == null)
                return Loc(TranslationKeys.SummarySelectPartFirst);

            var builder = new StringBuilder();
            builder.AppendLine(Loc(TranslationKeys.SummaryCadHeader));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadNumber), Safe(cad.CadNumber)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadRevision), Safe(cad.Revision)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadState), Safe(cad.State)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadClassification), Safe(cad.Classification)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadNativeFile), cad.HasNativeFile ? Loc(TranslationKeys.SummaryAvailable) : Loc(TranslationKeys.SummaryMissing)));
            builder.AppendLine(string.Format(Loc(TranslationKeys.SummaryCadLocked), cad.IsLocked ? Loc(TranslationKeys.SummaryYes) : Loc(TranslationKeys.SummaryNo)));
            builder.Append(string.Format(Loc(TranslationKeys.SummaryCadLockedBy), Safe(cad.LockedBy)));
            return builder.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string Loc(string key)
        {
            return LocalizationSource.Instance[key];
        }

        private static string Ttl => Loc(TranslationKeys.StartupErrorTitle);

        // The search screen only has flat Part rows plus classification.
        // It does not know the business-tree/root position, so we block
        // component-CAD creation for assembly-classified rows in this screen.
        private bool IsSelectedSearchAssemblyCandidate()
        {
            return SelectedSearchResult?.Part != null
                && CadNodeHelper.IsAssemblyClassification(SelectedSearchResult.Part.PartType);
        }

        private async Task ExecuteStartNewRevisionAsync()
        {
            if (_currentCad == null) return;

            var request = new PdmReviseRequest
            {
                PartId = SelectedSearchResult?.Part?.Id,
                CadId = _currentCad.Id,
                PartNumber = SelectedSearchResult?.Part?.PartNumber,
                CadNumber = _currentCad.CadNumber
            };

            var result = await _revisionService.ReviseAsync(request, CancellationToken.None);
            if (result.Success)
            {
                StatusMessage = string.Format(Loc(TranslationKeys.StatusNewRevisionCreated), result.NewRevision ?? "-");
                ResetSelectionState();
                ExecuteSearchAsync(_currentPage);
            }
            else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                StatusMessage = result.ErrorMessage;
            }
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
            ((RelayCommand)AddSelectedPartToLibraryCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StartNewRevisionCommand).RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
