using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Ui.ViewModels
{
    public sealed class LoginViewModel : INotifyPropertyChanged
    {
        private string _serverUrl;
        private string _database;
        private string _userName;
        private string _password;
        private string _errorMessage;
        private string _connectionMessage;
        private string _connectionDetails;
        private string _searchKeyword;
        private string _searchStatusMessage;
        private bool _isBusy;
        private bool _isConnected;
        private PartSearchResult _selectedSearchResult;

        public LoginViewModel()
            : this(new ArasClientOptions())
        {
        }

        public LoginViewModel(ArasClientOptions options)
        {
            var resolvedOptions = options ?? new ArasClientOptions();
            _serverUrl = resolvedOptions.BaseUri == null ? string.Empty : resolvedOptions.BaseUri.AbsoluteUri;
            _database = resolvedOptions.Database ?? string.Empty;
            SearchResults = new ObservableCollection<PartSearchResult>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ServerUrl
        {
            get { return _serverUrl; }
            set
            {
                if (_serverUrl == value)
                {
                    return;
                }

                _serverUrl = value;
                ClearConnectionState();
                OnPropertyChanged();
            }
        }

        public string Database
        {
            get { return _database; }
            set
            {
                if (_database == value)
                {
                    return;
                }

                _database = value;
                ClearConnectionState();
                OnPropertyChanged();
            }
        }

        public string UserName
        {
            get { return _userName; }
            set
            {
                if (_userName == value)
                {
                    return;
                }

                _userName = value;
                ClearConnectionState();
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get { return _password; }
            set
            {
                if (_password == value)
                {
                    return;
                }

                _password = value;
                ClearConnectionState();
                OnPropertyChanged();
            }
        }

        public string SearchKeyword
        {
            get { return _searchKeyword; }
            set
            {
                if (_searchKeyword == value)
                {
                    return;
                }

                _searchKeyword = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                if (_errorMessage == value)
                {
                    return;
                }

                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionMessage
        {
            get { return _connectionMessage; }
            set
            {
                if (_connectionMessage == value)
                {
                    return;
                }

                _connectionMessage = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionDetails
        {
            get { return _connectionDetails; }
            set
            {
                if (_connectionDetails == value)
                {
                    return;
                }

                _connectionDetails = value;
                OnPropertyChanged();
            }
        }

        public string SearchStatusMessage
        {
            get { return _searchStatusMessage; }
            set
            {
                if (_searchStatusMessage == value)
                {
                    return;
                }

                _searchStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                if (_isConnected == value)
                {
                    return;
                }

                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PartSearchResult> SearchResults { get; private set; }

        public PartSearchResult SelectedSearchResult
        {
            get { return _selectedSearchResult; }
            set
            {
                if (_selectedSearchResult == value)
                {
                    return;
                }

                _selectedSearchResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedResultDetails));
            }
        }

        public string SelectedResultDetails
        {
            get { return BuildSelectedResultDetails(SelectedSearchResult); }
        }

        public ArasLoginRequest CreateRequest()
        {
            return new ArasLoginRequest
            {
                ServerUrl = ServerUrl,
                Database = Database,
                UserName = UserName,
                Password = Password
            };
        }

        public void SetSearchResults(IReadOnlyList<PartSearchResult> results, string keyword)
        {
            SearchResults.Clear();

            if (results != null)
            {
                foreach (var result in results)
                {
                    SearchResults.Add(result);
                }
            }

            SelectedSearchResult = SearchResults.Count > 0 ? SearchResults[0] : null;
            SearchStatusMessage = BuildSearchStatusMessage(SearchResults.Count, keyword);
        }

        public void ClearSearchResults()
        {
            SearchResults.Clear();
            SelectedSearchResult = null;
            SearchStatusMessage = string.Empty;
        }

        private void ClearConnectionState()
        {
            ErrorMessage = string.Empty;
            ConnectionMessage = string.Empty;
            ConnectionDetails = string.Empty;
            IsConnected = false;
            ClearSearchResults();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private static string BuildSearchStatusMessage(int resultCount, string keyword)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                return string.Format(
                    TranslationResources.GetString(culture, TranslationKeys.LoginStatusPartSearch),
                    resultCount,
                    keyword.Trim());
            }

            return string.Format(
                TranslationResources.GetString(culture, TranslationKeys.StatusFoundResults),
                resultCount, 1, 1);
        }

        private static string BuildSelectedResultDetails(PartSearchResult result)
        {
            var culture = CultureInfo.CurrentUICulture.Name;
            var L = TranslationResources.GetString;

            if (result == null || result.Part == null)
            {
                return L(culture, TranslationKeys.LoginSelectedPartNone);
            }

            var builder = new StringBuilder();
            builder.AppendLine("Part");
            builder.AppendLine("  Id: " + Safe(culture, result.Part.Id));
            builder.AppendLine("  Number: " + Safe(culture, result.Part.PartNumber));
            builder.AppendLine("  Name: " + Safe(culture, result.Part.Name));
            builder.AppendLine("  Revision: " + Safe(culture, result.Part.Revision));
            builder.AppendLine("  State: " + Safe(culture, result.Part.State));
            builder.AppendLine("  Type: " + Safe(culture, result.Part.PartType));
            builder.AppendLine("  Description: " + Safe(culture, result.Part.Description));

            builder.AppendLine();
            builder.AppendLine(L(culture, TranslationKeys.LoginResultDetailsIronCad));
            if (result.IronCadPartCad == null)
            {
                builder.Append("  " + L(culture, TranslationKeys.LoginCadNoneLinked));
                return builder.ToString();
            }

            builder.AppendLine("  Id: " + Safe(culture, result.IronCadPartCad.Id));
            builder.AppendLine("  Number: " + Safe(culture, result.IronCadPartCad.CadNumber));
            builder.AppendLine("  Classification: " + Safe(culture, result.IronCadPartCad.Classification));
            builder.AppendLine("  Revision: " + Safe(culture, result.IronCadPartCad.Revision));
            builder.AppendLine("  State: " + Safe(culture, result.IronCadPartCad.State));
            builder.AppendLine("  Generation: " + result.IronCadPartCad.Generation);
            builder.AppendLine("  Native file id: " + Safe(culture, result.IronCadPartCad.NativeFileId));
            builder.AppendLine("  Has native file: " + (result.IronCadPartCad.HasNativeFile
                ? L(culture, TranslationKeys.LoginYesLabel)
                : L(culture, TranslationKeys.LoginNoLabel)));
            builder.AppendLine("  Locked: " + (result.IronCadPartCad.IsLocked
                ? L(culture, TranslationKeys.LoginYesLabel)
                : L(culture, TranslationKeys.LoginNoLabel)));
            builder.Append("  Locked by: " + Safe(culture, result.IronCadPartCad.LockedBy));
            return builder.ToString();
        }

        private static string Safe(string culture, string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? TranslationResources.GetString(culture, TranslationKeys.LoginEmptyLabel)
                : value;
        }
    }
}
