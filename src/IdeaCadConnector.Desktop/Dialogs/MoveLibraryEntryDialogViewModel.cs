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
    internal sealed class MoveLibraryEntryDialogViewModel : INotifyPropertyChanged
    {
        private readonly IPartLibraryClient _client;
        private readonly PartLibraryEntrySummary _entry;
        private readonly string _currentLibraryId;
        private PartLibrarySummary _selectedTargetLibrary;
        private bool _isBusy;
        private string _errorMessage;
        private bool _hasLoadedTargets;

        public MoveLibraryEntryDialogViewModel(IPartLibraryClient client, PartLibraryEntrySummary entry, string currentLibraryId)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _currentLibraryId = currentLibraryId ?? throw new ArgumentNullException(nameof(currentLibraryId));
            _errorMessage = string.Empty;

            TargetLibraries = new ObservableCollection<PartLibrarySummary>();

            MoveCommand = new RelayCommand(_ => _ = ExecuteMoveAsync(), _ => CanMove);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public string PartNumber => _entry.PartNumber;
        public string PartName => _entry.PartName;
        public string CurrentLibraryName => _entry.LibraryName;
        public string EntryStatus => _entry.EntryStatus.ToString();
        public string LifecycleState => _entry.LifecycleState;
        public string RevisionPolicy => _entry.RevisionPolicy.ToString();

        public ObservableCollection<PartLibrarySummary> TargetLibraries { get; }

        public PartLibrarySummary SelectedTargetLibrary
        {
            get => _selectedTargetLibrary;
            set
            {
                if (SetField(ref _selectedTargetLibrary, value))
                    RaiseMoveCommandState();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                    RaiseMoveCommandState();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetField(ref _errorMessage, value);
        }

        public bool HasLoadedTargets
        {
            get => _hasLoadedTargets;
            private set => SetField(ref _hasLoadedTargets, value);
        }

        public bool CanMove =>
            !IsBusy &&
            SelectedTargetLibrary != null &&
            !string.Equals(SelectedTargetLibrary.Id, _currentLibraryId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(SelectedTargetLibrary.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase);

        public bool IsSameLibrary =>
            SelectedTargetLibrary != null &&
            string.Equals(SelectedTargetLibrary.Id, _currentLibraryId, StringComparison.OrdinalIgnoreCase);

        public bool IsTargetArchived =>
            SelectedTargetLibrary != null &&
            string.Equals(SelectedTargetLibrary.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase);

        public ICommand MoveCommand { get; }
        public ICommand CancelCommand { get; }

        public MoveLibraryEntryResult MoveResult { get; private set; }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var libraries = await _client.GetLibrariesAsync(LibraryVisibilityFilter.Active, CancellationToken.None).ConfigureAwait(true);
                TargetLibraries.Clear();

                foreach (var library in libraries
                    .Where(item =>
                        item != null &&
                        !string.Equals(item.Id, _currentLibraryId, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(item.Status, PartLibrarySchemaNames.LibraryStatusArchived, StringComparison.OrdinalIgnoreCase) &&
                        item.CanContribute))
                {
                    TargetLibraries.Add(library);
                }

                HasLoadedTargets = true;

                if (TargetLibraries.Count > 0)
                {
                    SelectedTargetLibrary = TargetLibraries[0];
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.MoveEntryPermissionDenied);
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

        private async Task ExecuteMoveAsync()
        {
            if (!CanMove)
            {
                ErrorMessage = L(TranslationKeys.MoveEntryNoValidTargetsMessage);
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var request = new MoveLibraryEntryRequest
                {
                    EntryId = _entry.EntryId,
                    TargetLibraryId = SelectedTargetLibrary.Id
                };

                var result = await _client.MoveLibraryEntryAsync(request, CancellationToken.None).ConfigureAwait(true);
                MoveResult = result;

                if (result?.Success == true)
                {
                    CloseRequested?.Invoke(true);
                }
                else if (result?.ErrorCode == ArasErrorCode.PermissionDenied)
                {
                    ErrorMessage = L(TranslationKeys.MoveEntryPermissionDenied);
                }
                else
                {
                    ErrorMessage = result?.ErrorMessage ?? L(TranslationKeys.MoveEntryMoveFailed);
                }
            }
            catch (ArasOperationException ex) when (ex.ErrorCode == ArasErrorCode.PermissionDenied)
            {
                ErrorMessage = L(TranslationKeys.MoveEntryPermissionDenied);
            }
            catch (Exception ex)
            {
                ErrorMessage = L(TranslationKeys.MoveEntryMoveFailed) + " " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseMoveCommandState()
        {
            OnPropertyChanged(nameof(CanMove));
            OnPropertyChanged(nameof(IsSameLibrary));
            OnPropertyChanged(nameof(IsTargetArchived));
            (MoveCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
