using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    internal sealed class CreateLibraryDialogViewModel : INotifyPropertyChanged
    {
        private readonly IPartLibraryClient _client;
        private string _name;
        private string _description;
        private LibraryType _selectedType;
        private LibraryRevisionPolicy _defaultRevisionPolicy;
        private bool _isPublic;
        private bool _isBusy;
        private string _validationMessage;

        public CreateLibraryDialogViewModel(IPartLibraryClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _name = string.Empty;
            _description = string.Empty;
            _selectedType = LibraryType.Team;
            _defaultRevisionPolicy = LibraryRevisionPolicy.LatestCurrent;
            _isPublic = false;
            _validationMessage = string.Empty;
            LibraryTypes = new ObservableCollection<LibraryType>((LibraryType[])Enum.GetValues(typeof(LibraryType)));
            RevisionPolicies = new ObservableCollection<LibraryRevisionPolicy>((LibraryRevisionPolicy[])Enum.GetValues(typeof(LibraryRevisionPolicy)));

            SaveCommand = new RelayCommand(_ => _ = ExecuteSaveAsync(), _ => CanSave);
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event Action<bool> CloseRequested;

        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                {
                    RaiseSaveCommandState();
                }
            }
        }

        public string Description
        {
            get => _description;
            set => SetField(ref _description, value);
        }

        public LibraryType SelectedType
        {
            get => _selectedType;
            set => SetField(ref _selectedType, value);
        }

        public LibraryRevisionPolicy DefaultRevisionPolicy
        {
            get => _defaultRevisionPolicy;
            set => SetField(ref _defaultRevisionPolicy, value);
        }

        public bool IsPublic
        {
            get => _isPublic;
            set => SetField(ref _isPublic, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetField(ref _isBusy, value))
                    RaiseSaveCommandState();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetField(ref _validationMessage, value);
        }

        public ObservableCollection<LibraryType> LibraryTypes { get; }

        public ObservableCollection<LibraryRevisionPolicy> RevisionPolicies { get; }

        public bool CanSave => !IsBusy && !string.IsNullOrWhiteSpace(Name);

        public ICommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        public LibraryMutationResult SaveResult { get; private set; }

        public string CreatedLibraryId { get; private set; }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            ValidationMessage = string.Empty;
            await Task.CompletedTask;
            IsBusy = false;
        }

        private async Task ExecuteSaveAsync()
        {
            if (!CanSave)
            {
                ValidationMessage = L(TranslationKeys.CreateLibraryNameRequired);
                return;
            }

            IsBusy = true;
            ValidationMessage = string.Empty;

            try
            {
                var request = new CreatePartLibraryRequest
                {
                    Name = Name.Trim(),
                    Description = Description?.Trim() ?? string.Empty,
                    LibraryType = SelectedType,
                    DefaultRevisionPolicy = DefaultRevisionPolicy.ToString(),
                    IsPublic = IsPublic
                };

                var result = await _client.CreateLibraryAsync(request, CancellationToken.None).ConfigureAwait(true);
                SaveResult = result;
                CreatedLibraryId = result?.LibraryId;

                if (result?.Success == true)
                {
                    CloseRequested?.Invoke(true);
                }
                else if (result?.ErrorCode == ArasErrorCode.PermissionDenied)
                {
                    ValidationMessage = L(TranslationKeys.CreateLibraryPermissionDenied);
                }
                else if (LooksLikeDuplicateName(result?.ErrorMessage))
                {
                    ValidationMessage = L(TranslationKeys.CreateLibraryDuplicateName);
                }
                else
                {
                    ValidationMessage = result?.ErrorMessage ?? L(TranslationKeys.UnknownError);
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
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

        private static bool LooksLikeDuplicateName(string message)
        {
            return !string.IsNullOrWhiteSpace(message)
                && message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
