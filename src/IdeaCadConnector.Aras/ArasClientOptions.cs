using System;
using System.Linq;
using IdeaCadConnector.Core.Configuration;

namespace IdeaCadConnector.Aras
{
    public sealed class ArasClientOptions
    {
        public Uri BaseUri { get; set; }

        public string Database { get; set; }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        public string VaultId { get; set; }

        public string OAuthClientId { get; set; } = "IOMApp";

        public string OAuthScope { get; set; } = "Innovator";

        public string IronCadExecutablePath { get; set; }

        public int DefaultMaxSearchResults { get; set; } = 20;

        internal ArasClientOptions Clone()
        {
            return new ArasClientOptions
            {
                BaseUri = BaseUri,
                Database = Database,
                Timeout = Timeout,
                VaultId = VaultId,
                OAuthClientId = OAuthClientId,
                OAuthScope = OAuthScope,
                IronCadExecutablePath = IronCadExecutablePath,
                DefaultMaxSearchResults = DefaultMaxSearchResults
            };
        }
    }

    public static class ArasClientOptionsFactory
    {
        private static readonly object _lock = new();
        private static bool _initialized;

        public static ArasClientOptions Current
        {
            get
            {
                LazyInitialize();
                return _current?.Clone();
            }
        }

        private static ArasClientOptions _current;

        public static EnvironmentConfigurationResult CurrentConfig
        {
            get
            {
                LazyInitialize();
                return _currentConfig;
            }
        }

        internal static bool IsInitialized => _initialized;

        private static EnvironmentConfigurationResult _currentConfig;

        public static void Initialize()
        {
            lock (_lock)
            {
                _currentConfig = EnvironmentConfigurationLoader.Load();
                _current = FromConfiguration(_currentConfig);
                _initialized = true;
            }
        }

        internal static void Reset()
        {
            lock (_lock)
            {
                _current = null;
                _currentConfig = null;
                _initialized = false;
            }
        }

        private static void LazyInitialize()
        {
            if (!_initialized)
                Initialize();
        }

        public static ArasClientOptions FromConfiguration(EnvironmentConfigurationResult configResult)
        {
            if (configResult == null)
                throw new ArgumentNullException(nameof(configResult));

            var envConfig = configResult.Configuration;
            if (envConfig == null)
            {
                configResult.Errors.Add("No environment configuration loaded.");
                return new ArasClientOptions();
            }

            var options = new ArasClientOptions();

            if (!string.IsNullOrWhiteSpace(envConfig.Aras?.BaseUrl))
            {
                var url = envConfig.Aras.BaseUrl.Trim();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    options.BaseUri = uri;
                }
                else
                {
                    configResult.Errors.Add(
                        $"Configuration 'aras.baseUrl' is not a valid absolute URI: '{TruncateForLog(url)}'.");
                }
            }
            else
            {
                configResult.Errors.Add(
                    "Configuration 'aras.baseUrl' is missing or empty. Set it to your Aras Innovator server URL.");
            }

            if (!string.IsNullOrWhiteSpace(envConfig.Aras?.Database))
            {
                options.Database = envConfig.Aras.Database.Trim();
            }
            else
            {
                configResult.Errors.Add(
                    "Configuration 'aras.database' is missing or empty. Set it to your Aras Innovator database name.");
            }

            if (!string.IsNullOrWhiteSpace(envConfig.Aras?.VaultId))
            {
                options.VaultId = envConfig.Aras.VaultId.Trim();
            }
            else
            {
                configResult.Warnings.Add(
                    "Configuration 'aras.vaultId' is missing or empty. Vault upload/download will be unavailable " +
                    "until this value is configured.");
            }

            if (!string.IsNullOrWhiteSpace(envConfig.Aras?.OAuthClientId))
            {
                options.OAuthClientId = envConfig.Aras.OAuthClientId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(envConfig.Aras?.OAuthScope))
            {
                options.OAuthScope = envConfig.Aras.OAuthScope.Trim();
            }

            if (envConfig.Aras?.DefaultMaxSearchResults.HasValue == true)
            {
                var val = envConfig.Aras.DefaultMaxSearchResults.Value;
                if (val > 0)
                {
                    options.DefaultMaxSearchResults = val;
                }
                else
                {
                    configResult.Warnings.Add(
                        "Configuration 'aras.defaultMaxSearchResults' must be a positive integer. Using default (20).");
                }
            }

            if (envConfig.Aras?.TimeoutSeconds.HasValue == true)
            {
                var val = envConfig.Aras.TimeoutSeconds.Value;
                if (val > 0)
                {
                    options.Timeout = TimeSpan.FromSeconds(val);
                }
                else
                {
                    configResult.Warnings.Add(
                        "Configuration 'aras.timeoutSeconds' must be a positive integer. Using default (30s).");
                }
            }

            if (!string.IsNullOrWhiteSpace(envConfig.Local?.IronCadExecutablePath))
            {
                options.IronCadExecutablePath = envConfig.Local.IronCadExecutablePath.Trim();
            }

            return options;
        }

        public static ArasClientOptions WithLoginOverrides(
            this ArasClientOptions options,
            string serverUrl,
            string database)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var result = new ArasClientOptions
            {
                Timeout = options.Timeout,
                VaultId = options.VaultId,
                OAuthClientId = options.OAuthClientId,
                OAuthScope = options.OAuthScope,
                IronCadExecutablePath = options.IronCadExecutablePath,
                DefaultMaxSearchResults = options.DefaultMaxSearchResults
            };

            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                if (Uri.TryCreate(serverUrl.Trim(), UriKind.Absolute, out var uri))
                {
                    result.BaseUri = uri;
                }
            }

            result.Database = database?.Trim() ?? options.Database;

            return result;
        }

        private static string TruncateForLog(string value)
        {
            const int maxLen = 120;
            return value != null && value.Length > maxLen
                ? value.Substring(0, maxLen) + "..."
                : value;
        }
    }
}
