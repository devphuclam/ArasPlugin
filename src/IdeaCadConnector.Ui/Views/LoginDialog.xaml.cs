using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Ui.ViewModels;

namespace IdeaCadConnector.Ui.Views
{
    public partial class LoginDialog : Window
    {
        private ArasCadClient _arasClient;
        private readonly ArasClientOptions _options;

        public LoginDialog()
            : this(ArasClientOptionsFactory.Current ?? new ArasClientOptions())
        {
        }

        public LoginDialog(ArasClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            InitializeComponent();
            DataContext = new LoginViewModel(options);
        }

        public LoginViewModel ViewModel
        {
            get { return (LoginViewModel)DataContext; }
        }

        public ArasLoginRequest LoginRequest { get; private set; }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            ViewModel.Password = passwordBox == null ? string.Empty : passwordBox.Password;
        }

        private async void OnLoginClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsBusy)
            {
                return;
            }

            ViewModel.ErrorMessage = string.Empty;
            ViewModel.ConnectionMessage = string.Empty;
            ViewModel.ConnectionDetails = string.Empty;

            if (string.IsNullOrWhiteSpace(ViewModel.ServerUrl))
            {
                ViewModel.ErrorMessage = "Server URL is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.Database))
            {
                ViewModel.ErrorMessage = "Database is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.UserName))
            {
                ViewModel.ErrorMessage = "User name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.Password))
            {
                ViewModel.ErrorMessage = "Password is required.";
                return;
            }

            LoginRequest = ViewModel.CreateRequest();
            await TestConnectionAsync().ConfigureAwait(true);
        }

        private async void OnSearchClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsBusy || !ViewModel.IsConnected || _arasClient == null)
            {
                return;
            }

            ViewModel.ErrorMessage = string.Empty;
            await SearchPartsAsync().ConfigureAwait(true);
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            DisposeAuthenticatedClient();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeAuthenticatedClient();
            base.OnClosed(e);
        }

        private async Task TestConnectionAsync()
        {
            SetBusy(true);
            ViewModel.ConnectionMessage = "Connecting to Aras Innovator...";
            ViewModel.SearchStatusMessage = string.Empty;
            ViewModel.ClearSearchResults();

            try
            {
                DisposeAuthenticatedClient();

                var mergedOptions = _options.WithLoginOverrides(LoginRequest.ServerUrl, LoginRequest.Database);
                _arasClient = new ArasCadClient(mergedOptions);

                var loginResult = await _arasClient.LoginAsync(LoginRequest, CancellationToken.None).ConfigureAwait(true);
                ViewModel.IsConnected = true;
                ViewModel.ConnectionMessage = "Connected to Aras Innovator.";

                var searchResults = await SearchPartsCoreAsync().ConfigureAwait(true);
                ViewModel.ConnectionDetails = BuildSuccessDetails(loginResult, LoginRequest, searchResults.Items.Count);
            }
            catch (Exception ex)
            {
                DisposeAuthenticatedClient();
                ViewModel.IsConnected = false;
                ViewModel.ConnectionMessage = string.Empty;
                ViewModel.ConnectionDetails = string.Empty;
                ViewModel.ClearSearchResults();
                ViewModel.ErrorMessage = BuildFailureMessage(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SearchPartsAsync()
        {
            SetBusy(true);
            ViewModel.SearchStatusMessage = "Loading Part results from Aras...";

            try
            {
                await SearchPartsCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ViewModel.ErrorMessage = BuildFailureMessage(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task<PartSearchResponse> SearchPartsCoreAsync()
        {
            var response = await _arasClient.SearchPartsAsync(
                new PartSearchRequest
                {
                    Keyword = ViewModel.SearchKeyword,
                    MaxResults = 20
                },
                CancellationToken.None).ConfigureAwait(true);

            ViewModel.SetSearchResults(response.Items, ViewModel.SearchKeyword);
            return response;
        }

        private void SetBusy(bool isBusy)
        {
            ViewModel.IsBusy = isBusy;
            LoginButton.IsEnabled = !isBusy;
            SearchButton.IsEnabled = !isBusy && ViewModel.IsConnected;
            RefreshButton.IsEnabled = !isBusy && ViewModel.IsConnected;
            CloseButton.IsEnabled = !isBusy;
        }

        private static string BuildSuccessDetails(
            ArasLoginResult loginResult,
            ArasLoginRequest loginRequest,
            int sampleResultCount)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Server: " + Safe(loginRequest.ServerUrl));
            builder.AppendLine("Database: " + Safe(loginResult.Database));
            builder.AppendLine("User: " + Safe(loginResult.UserName));
            builder.AppendLine("Token type: " + Safe(loginResult.TokenType));
            builder.Append("Authenticated OData check: Part search returned ");
            builder.Append(sampleResultCount);
            builder.Append(" result(s).");
            return builder.ToString();
        }

        private static string BuildFailureMessage(Exception ex)
        {
            var builder = new StringBuilder("Connection failed.");
            var current = ex;
            var depth = 0;

            while (current != null && depth < 5)
            {
                builder.AppendLine();
                if (depth > 0)
                {
                    builder.Append("Inner error: ");
                }

                builder.Append(current.Message);
                current = current.InnerException;
                depth++;
            }

            return builder.ToString();
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
        }

        private void DisposeAuthenticatedClient()
        {
            if (_arasClient != null)
            {
                _arasClient.Dispose();
                _arasClient = null;
            }
        }
    }
}
