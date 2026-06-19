using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aras.IOM;
using Aras.IOM.OAuth;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Aras
{
    /// <summary>
    /// Manages an IOM Innovator session (via OAuth password grant) and exposes
    /// the <see cref="Innovator"/> instance together with the OAuth access token
    /// needed by the OData-based Part search client.
    /// </summary>
    public sealed class ArasAuthenticator : IDisposable
    {
        private readonly ArasClientOptions _options;
        private readonly ILogger<ArasAuthenticator> _logger;
        private HttpServerConnection _connection;
        private bool _disposed;

        public ArasAuthenticator(ArasClientOptions options, ILogger<ArasAuthenticator> logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArasAuthenticator>.Instance;
        }

        /// <summary>Authenticated IOM Innovator session — use this for all IOM operations.</summary>
        public Innovator Innovator { get; private set; }

        /// <summary>OAuth bearer token for OData HTTP requests.</summary>
        public string AccessToken { get; private set; }

        /// <summary>Token type (typically "Bearer").</summary>
        public string TokenType { get; private set; }

        /// <summary>
        /// Authenticate against the Aras server using OAuth password grant.
        /// On success the <see cref="Innovator"/> and <see cref="AccessToken"/>
        /// properties are populated.
        /// </summary>
        public async Task LoginAsync(string userName, string password, string database, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "User name is required.");
            if (string.IsNullOrWhiteSpace(password))
                throw new ArasOperationException(ArasErrorCode.ValidationFailed, "Password is required.");

            var serverUrl = _options.BaseUri.ToString().TrimEnd('/');
            var tokenEndpoint = _options.BaseUri + "oauthserver/connect/token";

            _logger.LogInformation("IOM login user={User} database={Database}", userName, database);

            try
            {
                // --- 1. IOM: create connection and log in ---
                var tokenProviderOptions = new PasswordTokenProviderOptions
                {
                    ClientId = _options.OAuthClientId,
                    Scope = _options.OAuthScope,
                    TokenEndpoint = tokenEndpoint,
                    UserName = userName,
                    Password = password,
                    Database = database
                };

                var tokenProvider = new PasswordTokenProvider(tokenProviderOptions);
                _connection = IomFactory.CreateHttpServerConnection(serverUrl, tokenProvider, ProtocolType.Standard);

                var loginResult = await Task.Run(() => _connection.Login(), ct);
                if (loginResult.isError())
                    throw new ArasOperationException(
                        ArasErrorCode.AuthInvalid,
                        "IOM login failed: " + loginResult.getErrorString());

                Innovator = IomFactory.CreateInnovator(_connection);

                _logger.LogInformation("IOM login succeeded user={User}", userName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArasOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IOM login failed user={User}", userName);
                throw new ArasOperationException(
                    ArasErrorCode.AuthInvalid,
                    "IOM authentication failed: " + ex.Message,
                    innerException: ex);
            }

            // --- 2. Obtain OAuth access token for OData by calling the token endpoint ---
            try
            {
                using var httpClient = new HttpClient { BaseAddress = _options.BaseUri, Timeout = _options.Timeout };

                var formFields = new Dictionary<string, string>
                {
                    { "grant_type", "password" },
                    { "client_id", _options.OAuthClientId },
                    { "scope", _options.OAuthScope },
                    { "database", database },
                    { "username", userName },
                    { "password", password }
                };

                using var tokenResponse = await httpClient.PostAsync(
                    "oauthserver/connect/token",
                    new FormUrlEncodedContent(formFields),
                    ct);

                var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    // IOM login succeeded but the token endpoint failed — this is unexpected
                    // but non-fatal: OData search will fail later.
                    _logger.LogWarning(
                        "OData token request failed (HTTP {Status}), OData search will be unavailable.",
                        (int)tokenResponse.StatusCode);
                    AccessToken = null;
                    TokenType = "Bearer";
                }
                else
                {
                    // Simple JSON parsing
                    var tokenData = JObject.Parse(tokenBody);
                    AccessToken = tokenData["access_token"]?.Value<string>();
                    TokenType = tokenData["token_type"]?.Value<string>() ?? "Bearer";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OData token fetch failed; OData search will be unavailable.");
                AccessToken = null;
                TokenType = "Bearer";
            }
        }

        /// <summary>Log out and release the IOM connection.</summary>
        public void Logout()
        {
            if (_connection != null)
            {
                try { _connection.Logout(); }
                catch { /* best-effort */ }
            }
            Innovator = null;
            AccessToken = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Logout();
                _disposed = true;
            }
        }
    }
}
