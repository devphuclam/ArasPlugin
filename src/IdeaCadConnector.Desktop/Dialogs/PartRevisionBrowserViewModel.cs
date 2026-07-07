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
    internal sealed class PartRevisionBrowserViewModel : INotifyPropertyChanged
    {
        private readonly IPartLibraryClient _client;
        private readonly PartLibraryEntryRow _entry;
        private readonly string _currentRevisionPolicy;
        private int _pageNumber = 1;
        private int _pageSize = 25;
        private int _totalCount;
        private PartRevisionHistoryItem _selectedRevision;
        private bool _isBusy;
        private bool _isPinning;
        private string _statusMessage;
        private string _errorMessage;
        private bool _hasLoaded;
        private bool _pinSuccess;

        public PartRevisionBrowserViewModel(IPartLibraryClient client, PartLibraryEntryRow entry, string currentRevisionPolicy)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _currentRevisionPolicy = currentRevisionPolicy ?? string.Empty;
            _statusMessage = string.Empty;
            _errorMessage = string.Empty;

            Revisions = new ObservableCollection<PartRevisionHistoryItem>();
            PageSizeOptions = new ObservableCollection<int>(new[] { 25, 50, 100 });

            RefreshCommand = new RelayCommand(_ => _ = LoadRevisionsAsync(), _ => !IsBusy);
            PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(_pageNumber - 1), _ => !IsBusy && _pageNumber > 1 && HasLoaded);
            NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(_pageNumber + 1), _ => !IsBusy && HasLoaded && _pageNumber * _pageSize < _totalCount);
            PinCommand = new RelayCommand(_ => _ = ExecutePinAsync(), _ => CanPin);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public string PartNumber => _entry.PartNumber;
        public string PartName => _entry.PartName;
        public string ConfigId => _entry.PartConfigId;
        public string CurrentRevisionPolicy => _currentRevisionPolicy;

        public ObservableCollection<PartRevisionHistoryItem> Revisions { get; }

        public ObservableCollection<int> PageSizeOptions { get; }

        public PartRevisionHistoryItem SelectedRevision
        {
            get => _selectedRevision;
            set
            {
                if (SetField(ref _selectedRevision, value))
                    RaisePinCommandState();
            }
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
                    _ = LoadRevisionsAsync();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                    RaiseCommandStates();
            }
        }

        public bool IsPinning
        {
            get => _isPinning;
            private set
            {
                if (SetField(ref _isPinning, value))
                    RaisePinCommandState();
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

        public bool HasLoaded
        {
            get => _hasLoaded;
            private set
            {
                if (SetField(ref _hasLoaded, value))
                    RaiseCommandStates();
            }
        }

        public bool PinSuccess
        {
            get => _pinSuccess;
            private set => SetField(ref _pinSuccess, value);
        }

        public int PageNumber => _pageNumber;
        public int PageSize => _pageSize;
        public int TotalCount => _totalCount;

        public bool HasNoConfigId => string.IsNullOrWhiteSpace(ConfigId);

        public bool CanPin =>
            !IsBusy &&
            !IsPinning &&
            SelectedRevision != null &&
            SelectedRevision.CanPin &&
            !HasNoConfigId;

        public string ShowPageText => HasLoaded
            ? string.Format(L(TranslationKeys.RevisionBrowserShowPage), _pageNumber, _totalCount)
            : string.Empty;

        public ICommand RefreshCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PinCommand { get; }
        public ICommand CancelCommand { get; }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            if (HasNoConfigId)
            {
                StatusMessage = L(TranslationKeys.RevisionBrowserNoConfigId);
                HasLoaded = true;
                IsBusy = false;
                return;
            }

            try
            {
                await LoadRevisionsCoreAsync().ConfigureAwait(true);
                HasLoaded = true;
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.RevisionBrowserPermissionDenied);
                HasLoaded = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                HasLoaded = true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadRevisionsAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                await LoadRevisionsCoreAsync().ConfigureAwait(true);
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.RevisionBrowserPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadRevisionsCoreAsync()
        {
            var request = new PartRevisionHistoryRequest
            {
                PartConfigId = ConfigId,
                PartId = _entry.PartId,
                PageNumber = _pageNumber,
                PageSize = Math.Min(100, Math.Max(1, _pageSize))
            };

            var response = await _client.SearchPartRevisionsAsync(request, CancellationToken.None).ConfigureAwait(true);

            Revisions.Clear();
            if (response?.Items != null)
            {
                foreach (var item in response.Items)
                {
                    Revisions.Add(item);
                }
            }

            _totalCount = response?.TotalCount ?? 0;
            _pageNumber = response?.PageNumber ?? 1;
            _pageSize = Math.Min(100, response?.PageSize ?? 25);

            OnPropertyChanged(nameof(PageNumber));
            OnPropertyChanged(nameof(PageSize));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ShowPageText));

            if (Revisions.Count == 0)
            {
                StatusMessage = L(TranslationKeys.RevisionBrowserNoRevisions);
            }

            HasLoaded = true;
        }

        private async Task GoToPageAsync(int page)
        {
            if (page < 1)
                return;

            _pageNumber = page;
            await LoadRevisionsAsync().ConfigureAwait(true);
        }

        private async Task ExecutePinAsync()
        {
            if (!CanPin || SelectedRevision == null)
                return;

            IsPinning = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            try
            {
                var request = new UpdateLibraryRevisionPolicyRequest
                {
                    EntryId = _entry.EntryId,
                    RevisionPolicy = LibraryRevisionPolicy.Pinned,
                    PinnedPartId = SelectedRevision.PartId
                };

                var result = await _client.UpdateRevisionPolicyAsync(request, CancellationToken.None).ConfigureAwait(true);

                if (result?.Success == true)
                {
                    PinSuccess = true;
                    StatusMessage = L(TranslationKeys.RevisionBrowserPinSuccess);
                }
                else
                {
                    ErrorMessage = result?.ErrorMessage ?? L(TranslationKeys.RevisionBrowserPinFailed);
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.RevisionBrowserPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = L(TranslationKeys.RevisionBrowserPinFailed) + " " + ex.Message;
            }
            finally
            {
                IsPinning = false;
            }
        }

        private void RaiseCommandStates()
        {
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            RaisePinCommandState();
        }

        private void RaisePinCommandState()
        {
            OnPropertyChanged(nameof(CanPin));
            (PinCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
