using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto.Library;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop
{
    internal sealed class ArasPartPickerViewModel : INotifyPropertyChanged
    {
        private readonly IPartLibraryClient _client;
        private string _keyword;
        private string _lifecycleState;
        private string _partType;
        private string _majorRev;
        private bool _currentOnly;
        private PartPickerSearchResultItem _selectedPart;
        private PartPreview _partPreview;
        private PartLibrarySummary _targetLibrary;
        private LibraryRevisionPolicy _revisionPolicy;
        private string _category;
        private string _tags;
        private string _note;
        private bool _isSearching;
        private bool _isAdding;
        private string _statusMessage;
        private string _errorMessage;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private int _totalCount;
        private bool _hasSearched;

        public ArasPartPickerViewModel(IPartLibraryClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _keyword = string.Empty;
            _lifecycleState = string.Empty;
            _partType = string.Empty;
            _majorRev = string.Empty;
            _revisionPolicy = LibraryRevisionPolicy.LatestCurrent;
            _category = string.Empty;
            _tags = string.Empty;
            _note = string.Empty;
            _statusMessage = string.Empty;
            _errorMessage = string.Empty;

            Libraries = new ObservableCollection<PartLibrarySummary>();
            SearchResults = new ObservableCollection<PartPickerSearchResultItem>();
            PageSizeOptions = new ObservableCollection<int>(new[] { 25, 50, 100 });
            RevisionPolicies = new ObservableCollection<LibraryRevisionPolicy>((LibraryRevisionPolicy[])Enum.GetValues(typeof(LibraryRevisionPolicy)));

            SearchCommand = new RelayCommand(_ => _ = ExecuteSearchAsync(), _ => !IsSearching && !IsAdding);
            PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(_pageNumber - 1), _ => !IsSearching && !IsAdding && _pageNumber > 1 && HasSearched);
            NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(_pageNumber + 1), _ => !IsSearching && !IsAdding && HasSearched && _pageNumber * _pageSize < _totalCount);
            AddCommand = new RelayCommand(_ => _ = ExecuteAddAsync(), _ => CanAdd);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public ObservableCollection<PartLibrarySummary> Libraries { get; }

        public ObservableCollection<PartPickerSearchResultItem> SearchResults { get; }

        public ObservableCollection<int> PageSizeOptions { get; }

        public ObservableCollection<LibraryRevisionPolicy> RevisionPolicies { get; }

        public string Keyword
        {
            get => _keyword;
            set => SetField(ref _keyword, value);
        }

        public string LifecycleState
        {
            get => _lifecycleState;
            set => SetField(ref _lifecycleState, value);
        }

        public string PartType
        {
            get => _partType;
            set => SetField(ref _partType, value);
        }

        public string MajorRev
        {
            get => _majorRev;
            set => SetField(ref _majorRev, value);
        }

        public bool CurrentOnly
        {
            get => _currentOnly;
            set => SetField(ref _currentOnly, value);
        }

        public int SelectedPageSize
        {
            get => _pageSize;
            set
            {
                var normalized = value <= 0 ? 25 : Math.Min(100, value);
                if (SetField(ref _pageSize, normalized, nameof(SelectedPageSize)))
                {
                    OnPropertyChanged(nameof(PageSize));
                    RaiseSearchCommandState();
                }
            }
        }

        public PartPickerSearchResultItem SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (SetField(ref _selectedPart, value))
                {
                    _ = LoadPreviewAsync();
                    RaiseAddCommandState();
                }
            }
        }

        public PartPreview PartPreview
        {
            get => _partPreview;
            private set => SetField(ref _partPreview, value);
        }

        public PartLibrarySummary TargetLibrary
        {
            get => _targetLibrary;
            set
            {
                if (SetField(ref _targetLibrary, value))
                    RaiseAddCommandState();
            }
        }

        public LibraryRevisionPolicy RevisionPolicy
        {
            get => _revisionPolicy;
            set => SetField(ref _revisionPolicy, value);
        }

        public string Category
        {
            get => _category;
            set => SetField(ref _category, value);
        }

        public string Tags
        {
            get => _tags;
            set => SetField(ref _tags, value);
        }

        public string Note
        {
            get => _note;
            set => SetField(ref _note, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (SetField(ref _isSearching, value))
                    RaiseSearchCommandState();
            }
        }

        public bool IsAdding
        {
            get => _isAdding;
            private set
            {
                if (SetField(ref _isAdding, value))
                    RaiseAddCommandState();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetField(ref _statusMessage, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetField(ref _errorMessage, value);
        }

        public int PageNumber => _pageNumber;

        public int PageSize => _pageSize;

        public int TotalCount => _totalCount;

        public bool IsTargetLibraryArchived =>
            string.Equals(TargetLibrary?.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase);

        public bool HasSearched
        {
            get => _hasSearched;
            private set => SetField(ref _hasSearched, value);
        }

        public string PagingSummary
        {
            get
            {
                if (_totalCount <= 0)
                    return string.Empty;

                return string.Format(
                    L(TranslationKeys.PartPickerShowingPage),
                    _pageNumber,
                    _totalCount);
            }
        }

        public bool CanAdd =>
            !IsAdding &&
            SelectedPart != null &&
            TargetLibrary != null &&
            !IsTargetLibraryArchived &&
            PartPreview != null &&
            PartPreview.IsEligibleForReuse &&
            !string.IsNullOrWhiteSpace(PartPreview.ConfigId);

        public ICommand SearchCommand { get; }

        public ICommand PreviousPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand AddCommand { get; }

        public ICommand CancelCommand { get; }

        public AddPartToLibraryResult AddResult { get; private set; }

        public async Task InitializeAsync()
        {
            IsSearching = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                var libraries = await _client.GetLibrariesAsync(LibraryVisibilityFilter.Active, CancellationToken.None).ConfigureAwait(true);
                Libraries.Clear();
                foreach (var library in libraries.Where(item =>
                    item != null &&
                    item.CanContribute &&
                    !string.Equals(item.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase)))
                {
                    Libraries.Add(library);
                }

                if (Libraries.Count > 0)
                {
                    TargetLibrary = Libraries[0];
                }
                else
                {
                    StatusMessage = L(TranslationKeys.PartPickerNoActiveLibraries);
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.PartPickerPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(L(TranslationKeys.PartPickerSearchFailed), ex.Message);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task ExecuteSearchAsync()
        {
            IsSearching = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;
            _pageNumber = 1;
            PartPreview = null;
            SelectedPart = null;

            try
            {
                var request = new PartPickerSearchRequest
                {
                    Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
                    LifecycleState = string.IsNullOrWhiteSpace(LifecycleState) ? null : LifecycleState.Trim(),
                    PartType = string.IsNullOrWhiteSpace(PartType) ? null : PartType.Trim(),
                    MajorRev = string.IsNullOrWhiteSpace(MajorRev) ? null : MajorRev.Trim(),
                    CurrentOnly = CurrentOnly ? true : (bool?)null,
                    PageNumber = _pageNumber,
                    PageSize = Math.Min(100, Math.Max(1, _pageSize))
                };

                var response = await _client.SearchPartsAsync(request, CancellationToken.None).ConfigureAwait(true);

                SearchResults.Clear();
                if (response?.Items != null)
                {
                    foreach (var item in response.Items)
                    {
                        SearchResults.Add(item);
                    }
                }

                _totalCount = response?.TotalCount ?? 0;
                _pageNumber = response?.PageNumber ?? 1;
                _pageSize = Math.Min(100, response?.PageSize ?? 25);
                HasSearched = true;

                OnPropertyChanged(nameof(PagingSummary));
                OnPropertyChanged(nameof(PageNumber));
                OnPropertyChanged(nameof(PageSize));
                OnPropertyChanged(nameof(TotalCount));
                RaiseSearchCommandState();

                if (SearchResults.Count == 0)
                {
                    StatusMessage = L(TranslationKeys.PartPickerNoResults);
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.PartPickerPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(L(TranslationKeys.PartPickerSearchFailed), ex.Message);
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async Task GoToPageAsync(int page)
        {
            if (page < 1)
                return;

            _pageNumber = page;
            await ExecuteSearchAsync().ConfigureAwait(true);
        }

        private async Task LoadPreviewAsync()
        {
            if (SelectedPart == null || string.IsNullOrWhiteSpace(SelectedPart.PartId))
            {
                PartPreview = null;
                RaiseAddCommandState();
                return;
            }

            try
            {
                var preview = await _client.GetPartPreviewAsync(SelectedPart.PartId, CancellationToken.None).ConfigureAwait(true);
                PartPreview = preview;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                PartPreview = new PartPreview
                {
                    PartId = SelectedPart.PartId,
                    IsEligibleForReuse = false,
                    IneligibilityReason = L(TranslationKeys.PartPickerPermissionDenied)
                };
            }
            catch (Exception ex)
            {
                PartPreview = new PartPreview
                {
                    PartId = SelectedPart.PartId,
                    IsEligibleForReuse = false,
                    IneligibilityReason = ex.Message
                };
            }
            finally
            {
                RaiseAddCommandState();
            }
        }

        private async Task ExecuteAddAsync()
        {
            if (SelectedPart == null)
            {
                StatusMessage = L(TranslationKeys.PartPickerSelectPartFirst);
                return;
            }

            if (TargetLibrary == null)
            {
                StatusMessage = L(TranslationKeys.PartPickerNoActiveLibraries);
                return;
            }

            if (IsTargetLibraryArchived)
            {
                StatusMessage = L(TranslationKeys.PartPickerArchivedTarget);
                return;
            }

            if (PartPreview == null || !PartPreview.IsEligibleForReuse)
            {
                StatusMessage = string.IsNullOrWhiteSpace(PartPreview?.IneligibilityReason)
                    ? L(TranslationKeys.PartPickerIneligible)
                    : PartPreview.IneligibilityReason;
                return;
            }

            if (string.IsNullOrWhiteSpace(PartPreview.ConfigId))
            {
                StatusMessage = L(TranslationKeys.PartPickerNoConfigId);
                return;
            }

            if (!CanAdd)
                return;

            IsAdding = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                var configId = PartPreview?.ConfigId ?? SelectedPart.ConfigId;
                var partId = PartPreview?.PartId ?? SelectedPart.PartId;

                if (!string.IsNullOrWhiteSpace(configId))
                {
                    var duplicateCheck = await _client.CheckDuplicateEntryAsync(
                        TargetLibrary.Id,
                        configId,
                        CancellationToken.None).ConfigureAwait(true);

                    if (duplicateCheck?.IsDuplicate == true)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(duplicateCheck.ExistingEntryId)
                            ? L(TranslationKeys.PartPickerDuplicateEntry)
                            : L(TranslationKeys.PartPickerDuplicateEntry) + " (" + duplicateCheck.ExistingEntryId + ")";
                        return;
                    }
                }

                var request = new AddPartToLibraryRequest
                {
                    LibraryId = TargetLibrary.Id,
                    PartId = partId,
                    PartConfigId = configId,
                    PartNumber = SelectedPart.PartNumber,
                    RevisionPolicy = RevisionPolicy,
                    Category = Category?.Trim() ?? string.Empty,
                    Tags = Tags?.Trim() ?? string.Empty,
                    Note = Note?.Trim() ?? string.Empty
                };

                var result = await _client.AddPartAsync(request, CancellationToken.None).ConfigureAwait(true);
                AddResult = result;

                if (result?.AlreadyExists == true)
                {
                    StatusMessage = string.IsNullOrWhiteSpace(result.EntryId)
                        ? L(TranslationKeys.PartPickerAlreadyExists)
                        : L(TranslationKeys.PartPickerAlreadyExists) + " (" + result.EntryId + ")";
                }
                else if (result?.Success == true)
                {
                    CloseRequested?.Invoke(true);
                }
                else
                {
                    StatusMessage = result?.ErrorMessage ?? L(TranslationKeys.UnknownError);
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.PartPickerPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format(L(TranslationKeys.PartPickerSearchFailed), ex.Message);
            }
            finally
            {
                IsAdding = false;
            }
        }

        private void RaiseSearchCommandState()
        {
            OnPropertyChanged(nameof(CanAdd));
            (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void RaiseAddCommandState()
        {
            OnPropertyChanged(nameof(CanAdd));
            (AddCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private static string L(string key)
        {
            return TranslationResources.GetString(CultureInfo.CurrentUICulture.Name, key);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
