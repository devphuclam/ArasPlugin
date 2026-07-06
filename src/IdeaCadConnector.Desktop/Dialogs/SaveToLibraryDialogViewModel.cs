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
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop
{
    internal sealed class SaveToLibraryDialogViewModel : INotifyPropertyChanged
    {
        private readonly IPartLibraryClient _client;
        private readonly PartLibrarySaveSeed _seed;
        private bool _isBusy;
        private PartLibrarySummary _selectedLibrary;
        private LibraryPolicyOption _selectedRevisionPolicy;
        private string _category;
        private string _tags;
        private string _note;
        private string _sourceProject;
        private string _sourceCommit;
        private string _validationMessage;

        public SaveToLibraryDialogViewModel(IPartLibraryClient client, PartLibrarySaveSeed seed)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _seed = seed ?? throw new ArgumentNullException(nameof(seed));

            WritableLibraries = new ObservableCollection<PartLibrarySummary>();
            RevisionPolicies = new ObservableCollection<LibraryPolicyOption>
            {
                new LibraryPolicyOption(LibraryRevisionPolicy.Pinned, L(TranslationKeys.SaveToLibraryPolicyPinned)),
                new LibraryPolicyOption(LibraryRevisionPolicy.LatestReleased, L(TranslationKeys.SaveToLibraryPolicyLatestReleased)),
                new LibraryPolicyOption(LibraryRevisionPolicy.LatestCurrent, L(TranslationKeys.SaveToLibraryPolicyLatestCurrent))
            };

            _selectedRevisionPolicy = RevisionPolicies.First(policy => policy.Policy == _seed.DefaultRevisionPolicy);
            _category = _seed.Category ?? string.Empty;
            _tags = _seed.Tags ?? string.Empty;
            _note = _seed.Note ?? string.Empty;
            _sourceProject = _seed.SourceProject ?? string.Empty;
            _sourceCommit = _seed.SourceCommit ?? string.Empty;
            _validationMessage = string.Empty;

            SaveCommand = new RelayCommand(_ => _ = ExecuteSaveAsync(), _ => CanSave);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public ObservableCollection<PartLibrarySummary> WritableLibraries { get; }

        public ObservableCollection<LibraryPolicyOption> RevisionPolicies { get; }

        public ICommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        public AddPartToLibraryResult SaveResult { get; private set; }

        public string SavedLibraryId { get; private set; }

        public string SelectedPartSummary
        {
            get
            {
                var partNumber = string.IsNullOrWhiteSpace(_seed.PartNumber) ? "-" : _seed.PartNumber.Trim();
                var partName = string.IsNullOrWhiteSpace(_seed.PartName) ? "-" : _seed.PartName.Trim();
                return partNumber + " - " + partName;
            }
        }

        public string SelectedPartId => string.IsNullOrWhiteSpace(_seed.PartId) ? "-" : _seed.PartId;

        public PartLibrarySummary SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetField(ref _selectedLibrary, value))
                {
                    RaiseSaveCommandState();
                }
            }
        }

        public LibraryPolicyOption SelectedRevisionPolicy
        {
            get => _selectedRevisionPolicy;
            set => SetField(ref _selectedRevisionPolicy, value);
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

        public string SourceProject
        {
            get => _sourceProject;
            set => SetField(ref _sourceProject, value);
        }

        public string SourceCommit
        {
            get => _sourceCommit;
            set => SetField(ref _sourceCommit, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                {
                    RaiseSaveCommandState();
                }
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetField(ref _validationMessage, value);
        }

        public bool CanSave =>
            !IsBusy &&
            SelectedLibrary != null &&
            !string.IsNullOrWhiteSpace(_seed.PartId);

        public async Task InitializeAsync()
        {
            IsBusy = true;
            ValidationMessage = string.Empty;

            try
            {
                var libraries = await _client.GetLibrariesAsync(LibraryVisibilityFilter.Active, CancellationToken.None).ConfigureAwait(true);
                WritableLibraries.Clear();
                foreach (var library in libraries.Where(item => item != null && item.CanContribute))
                {
                    WritableLibraries.Add(library);
                }

                if (WritableLibraries.Count > 0)
                {
                    SelectedLibrary = WritableLibraries[0];
                }
                else
                {
                    ValidationMessage = L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = string.Format(L(TranslationKeys.LibraryStatusPartSaveFailed), ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSaveAsync()
        {
            if (!CanSave)
            {
                ValidationMessage = string.IsNullOrWhiteSpace(_seed.PartId)
                    ? L(TranslationKeys.LibraryStatusPushPartToArasFirst)
                    : L(TranslationKeys.LibraryStatusSelectWritableLibrary);
                return;
            }

            IsBusy = true;
            ValidationMessage = string.Empty;

            try
            {
                var request = new AddPartToLibraryRequest
                {
                    LibraryId = SelectedLibrary.Id,
                    PartId = _seed.PartId,
                    PartConfigId = _seed.PartConfigId,
                    PartNumber = _seed.PartNumber,
                    RevisionPolicy = SelectedRevisionPolicy?.Policy ?? LibraryRevisionPolicy.LatestReleased,
                    Category = Category,
                    Tags = Tags,
                    Note = Note,
                    SourceProject = SourceProject,
                    SourceCommit = SourceCommit
                };

                var result = await _client.AddPartAsync(request, CancellationToken.None).ConfigureAwait(true);
                SaveResult = result;
                SavedLibraryId = request.LibraryId;

                if (result?.Success == true)
                {
                    CloseRequested?.Invoke(true);
                }
                else
                {
                    ValidationMessage = string.Format(
                        L(TranslationKeys.LibraryStatusPartSaveFailed),
                        result?.ErrorMessage ?? L(TranslationKeys.UnknownError));
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = string.Format(L(TranslationKeys.LibraryStatusPartSaveFailed), ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseSaveCommandState()
        {
            OnPropertyChanged(nameof(CanSave));
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    internal sealed class PartLibrarySaveSeed
    {
        public string PartId { get; set; }

        public string PartConfigId { get; set; }

        public string PartNumber { get; set; }

        public string PartName { get; set; }

        public string Category { get; set; }

        public string Tags { get; set; }

        public string Note { get; set; }

        public string SourceProject { get; set; }

        public string SourceCommit { get; set; }

        public LibraryRevisionPolicy DefaultRevisionPolicy { get; set; } = LibraryRevisionPolicy.LatestReleased;
    }

    internal sealed class LibraryPolicyOption
    {
        public LibraryPolicyOption(LibraryRevisionPolicy policy, string displayName)
        {
            Policy = policy;
            DisplayName = displayName;
        }

        public LibraryRevisionPolicy Policy { get; }

        public string DisplayName { get; }
    }
}
