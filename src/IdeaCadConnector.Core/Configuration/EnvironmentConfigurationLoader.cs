using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdeaCadConnector.Core.Configuration
{
    public static class EnvironmentConfigurationLoader
    {
        public const string EnvVarName = "IDEA_CAD_CONNECTOR_ENV_CONFIG";
        public const string FileName = "IdeaCadConnector.environment.json";
        public const int SupportedSchemaVersion = 1;

        private static readonly HashSet<string> SecretLikeKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "token", "secret", "cookie", "session", "credential",
            "passphrase", "auth", "apikey", "api_key"
        };

        public static EnvironmentConfigurationResult Load()
        {
            return Load(CreateProductionPathContext());
        }

        internal static EnvironmentConfigurationResult Load(EnvironmentConfigurationPathContext paths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            var result = new EnvironmentConfigurationResult();

            string path = ResolvePath(paths, result.Errors);
            if (path == null)
            {
                if (result.Errors.Count > 0)
                {
                    result.Configuration = CreateDefault();
                    result.SourcePath = paths.EnvironmentVariableValue;
                    return result;
                }

                result.Warnings.Add("No environment config file found. Using built-in defaults.");
                result.Configuration = CreateDefault();
                result.SourcePath = "built-in defaults";
                return result;
            }

            return LoadFromPath(path);
        }

        public static EnvironmentConfigurationResult LoadFromPath(string path)
        {
            var result = new EnvironmentConfigurationResult { SourcePath = path };

            if (!File.Exists(path))
            {
                result.Warnings.Add($"Config file not found: {path}. Using built-in defaults.");
                result.Configuration = CreateDefault();
                return result;
            }

            try
            {
                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                {
                    result.Warnings.Add("Config file is empty. Using built-in defaults.");
                    result.Configuration = CreateDefault();
                    return result;
                }

                var parsed = JObject.Parse(json);

                var secretWarnings = DetectSecretLikeKeys(parsed, "");
                foreach (var w in secretWarnings)
                {
                    result.Warnings.Add(w);
                }

                int schemaVersion = parsed.Value<int?>("schemaVersion") ?? 1;
                if (schemaVersion != SupportedSchemaVersion)
                {
                    result.Errors.Add($"Unsupported schemaVersion {schemaVersion}. Expected {SupportedSchemaVersion}. Using fallback defaults.");
                    result.Configuration = CreateDefault();
                    return result;
                }

                var config = parsed.ToObject<EnvironmentConfiguration>();
                ExpandEnvironmentVariables(config);
                result.Configuration = config;
            }
            catch (JsonReaderException ex)
            {
                result.Errors.Add($"Config file contains malformed JSON: {ex.Message}. Using built-in defaults.");
                result.Configuration = CreateDefault();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                result.Errors.Add($"Cannot read config file: {ex.Message}. Using built-in defaults.");
                result.Configuration = CreateDefault();
            }

            return result;
        }

        public static string ResolvePath()
        {
            var errors = new List<string>();
            return ResolvePath(CreateProductionPathContext(), errors);
        }

        internal static string ResolvePath(
            EnvironmentConfigurationPathContext paths,
            IList<string> errors)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            string envPath = paths.EnvironmentVariableValue;
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                string fullEnvPath = Path.GetFullPath(envPath.Trim());
                if (Directory.Exists(fullEnvPath))
                {
                    errors.Add("The explicit environment config path points to a directory. " +
                        "Set IDEA_CAD_CONNECTOR_ENV_CONFIG to a readable JSON file.");
                    return null;
                }

                if (!File.Exists(fullEnvPath))
                {
                    errors.Add("The explicit environment config path does not exist. " +
                        "Set IDEA_CAD_CONNECTOR_ENV_CONFIG to an existing readable JSON file.");
                    return null;
                }

                return fullEnvPath;
            }

            string nextToExe = Path.Combine(paths.SideBySideDirectory, FileName);
            if (File.Exists(nextToExe))
            {
                return Path.GetFullPath(nextToExe);
            }

            string appData = Path.Combine(paths.AppDataDirectory, FileName);
            if (File.Exists(appData))
            {
                return Path.GetFullPath(appData);
            }

            return null;
        }

        private static EnvironmentConfigurationPathContext CreateProductionPathContext()
        {
            return new EnvironmentConfigurationPathContext(
                Environment.GetEnvironmentVariable(EnvVarName),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IdeaCadConnector"));
        }

        public static EnvironmentConfiguration CreateDefault()
        {
            return new EnvironmentConfiguration();
        }

        private static void ExpandEnvironmentVariables(EnvironmentConfiguration config)
        {
            if (config.Local != null)
            {
                config.Local.VaultCacheDirectory = ExpandPath(config.Local.VaultCacheDirectory);
                config.Local.IronCadExecutablePath = ExpandPath(config.Local.IronCadExecutablePath);
            }

            if (config.Diagnostics != null)
            {
                config.Diagnostics.LogDirectory = ExpandPath(config.Diagnostics.LogDirectory);
            }
        }

        public static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            path = path.Replace("%LOCALAPPDATA%",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            path = path.Replace("%APPDATA%",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            path = path.Replace("%USERPROFILE%",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            return path;
        }

        private static List<string> DetectSecretLikeKeys(JToken token, string prefix)
        {
            var warnings = new List<string>();

            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    string fullPath = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

                    if (SecretLikeKeys.Contains(prop.Name))
                    {
                        warnings.Add($"Config contains potentially secret key '{fullPath}'. Consider removing from environment config.");
                    }

                    if (prop.Value is JObject || prop.Value is JArray)
                    {
                        warnings.AddRange(DetectSecretLikeKeys(prop.Value, fullPath));
                    }
                }
            }
            else if (token is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    warnings.AddRange(DetectSecretLikeKeys(arr[i], $"{prefix}[{i}]"));
                }
            }

            return warnings;
        }
    }
}
