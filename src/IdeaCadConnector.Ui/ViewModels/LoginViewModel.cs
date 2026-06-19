using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto;

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
            var builder = new StringBuilder();
            builder.Append("Part search returned ");
            builder.Append(resultCount);
            builder.Append(" result(s)");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                builder.Append(" for keyword \"");
                builder.Append(keyword.Trim());
                builder.Append("\"");
            }

            builder.Append('.');
            return builder.ToString();
        }

        private static string BuildSelectedResultDetails(PartSearchResult result)
        {
            if (result == null || result.Part == null)
            {
                return "No Part selected.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Part");
            builder.AppendLine("  Id: " + Safe(result.Part.Id));
            builder.AppendLine("  Number: " + Safe(result.Part.PartNumber));
            builder.AppendLine("  Name: " + Safe(result.Part.Name));
            builder.AppendLine("  Revision: " + Safe(result.Part.Revision));
            builder.AppendLine("  State: " + Safe(result.Part.State));
            builder.AppendLine("  Type: " + Safe(result.Part.PartType));
            builder.AppendLine("  Description: " + Safe(result.Part.Description));

            builder.AppendLine();
            builder.AppendLine("IronCAD Part CAD");
            if (result.IronCadPartCad == null)
            {
                builder.Append("  None linked in current query result.");
                return builder.ToString();
            }

            builder.AppendLine("  Id: " + Safe(result.IronCadPartCad.Id));
            builder.AppendLine("  Number: " + Safe(result.IronCadPartCad.CadNumber));
            builder.AppendLine("  Classification: " + Safe(result.IronCadPartCad.Classification));
            builder.AppendLine("  Revision: " + Safe(result.IronCadPartCad.Revision));
            builder.AppendLine("  State: " + Safe(result.IronCadPartCad.State));
            builder.AppendLine("  Generation: " + result.IronCadPartCad.Generation);
            builder.AppendLine("  Native file id: " + Safe(result.IronCadPartCad.NativeFileId));
            builder.AppendLine("  Has native file: " + (result.IronCadPartCad.HasNativeFile ? "Yes" : "No"));
            builder.AppendLine("  Locked: " + (result.IronCadPartCad.IsLocked ? "Yes" : "No"));
            builder.Append("  Locked by: " + Safe(result.IronCadPartCad.LockedBy));
            return builder.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
        }
    }
}
