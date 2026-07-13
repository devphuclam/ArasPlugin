using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Configuration;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Contracts;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using IdeaCadConnector.Desktop;
using IdeaCadConnector.Desktop.Services;
using Xunit;

namespace IdeaCadConnector.Tests
{
    [Collection("Environment configuration process state")]
    public sealed class EnvironmentConfigurationTests : IDisposable
    {
        private readonly string _tempDir;

        public EnvironmentConfigurationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "IdeaCadConnector_EnvConfigTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            ArasClientOptionsFactory.Reset();
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Fact]
        public void Load_WhenNoFileExists_ReturnsDefaultsAndWarning()
        {
            var result = EnvironmentConfigurationLoader.LoadFromPath(Path.Combine(_tempDir, "nonexistent.json"));
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("not found"));
            Assert.NotNull(result.Configuration);
        }

        [Fact]
        public void Load_WhenFileIsValidTemplate_ParsesSuccessfully()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""environmentName"": ""UAT"",
                ""aras"": {
                    ""baseUrl"": ""https://aras.example.com/InnovatorServer"",
                    ""database"": ""InnovatorSolutions""
                },
                ""local"": {
                    ""vaultCacheDirectory"": ""%LOCALAPPDATA%/IdeaCadConnector/VaultCache""
                },
                ""roles"": {
                    ""managerUsers"": [""TPTKC""],
                    ""reviewerUsers"": [""TNTKC""],
                    ""contributorUsers"": [""NVTKC""],
                    ""readOnlyUsers"": [""NVLCR"", ""PM""]
                }
            }";
            string path = Path.Combine(_tempDir, "valid.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            Assert.Equal("UAT", result.Configuration.EnvironmentName);
            Assert.Equal("https://aras.example.com/InnovatorServer", result.Configuration.Aras.BaseUrl);
            Assert.Equal("InnovatorSolutions", result.Configuration.Aras.Database);
            Assert.Contains("TPTKC", result.Configuration.Roles.ManagerUsers);
            Assert.Contains("TNTKC", result.Configuration.Roles.ReviewerUsers);
            Assert.Contains("NVTKC", result.Configuration.Roles.ContributorUsers);
            Assert.Contains("NVLCR", result.Configuration.Roles.ReadOnlyUsers);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentLocalAppDataExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%LOCALAPPDATA%/IdeaCadConnector/Logs");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%LOCALAPPDATA%", expanded);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentAppDataExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%APPDATA%/IdeaCadConnector");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%APPDATA%", expanded);
        }

        [Fact]
        public void Load_WhenPathExpansion_PercentUserProfileExpands()
        {
            string expanded = EnvironmentConfigurationLoader.ExpandPath("%USERPROFILE%/IdeaCadConnector");
            Assert.Contains("IdeaCadConnector", expanded);
            Assert.DoesNotContain("%USERPROFILE%", expanded);
        }

        [Fact]
        public void Load_WhenMalformedJson_ReturnsValidationError()
        {
            string path = Path.Combine(_tempDir, "malformed.json");
            File.WriteAllText(path, "{ this is not valid json }");

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("malformed JSON"));
        }

        [Fact]
        public void Load_WhenUnsupportedSchemaVersion_ReturnsError()
        {
            string json = @"{ ""schemaVersion"": 99 }";
            string path = Path.Combine(_tempDir, "badver.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("Unsupported schemaVersion"));
        }

        [Fact]
        public void Load_WhenEmptyFile_ReturnsWarningWithDefaults()
        {
            string path = Path.Combine(_tempDir, "empty.json");
            File.WriteAllText(path, "");

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("empty"));
        }

        [Fact]
        public void Load_WhenPasswordKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""password"": ""should-not-be-here""
                }
            }";
            string path = Path.Combine(_tempDir, "withpass.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("password"));
        }

        [Fact]
        public void Load_WhenTokenKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""token"": ""some-token""
                }
            }";
            string path = Path.Combine(_tempDir, "withtoken.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("token"));
        }

        [Fact]
        public void Load_WhenSecretKeyPresent_EmitsWarning()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""secret"": ""my-secret""
                }
            }";
            string path = Path.Combine(_tempDir, "withsecret.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Contains("secret"));
        }

        [Fact]
        public void Load_WhenRoleOverride_ManagerUsersMapsTptkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""managerUsers"": [""TPTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_mgr.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("TPTKC", result.Configuration.Roles.ManagerUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ReviewerUsersMapsTntkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""reviewerUsers"": [""TNTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_rev.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("TNTKC", result.Configuration.Roles.ReviewerUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ContributorUsersMapsNvtkc()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""contributorUsers"": [""NVTKC""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_contrib.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("NVTKC", result.Configuration.Roles.ContributorUsers);
        }

        [Fact]
        public void Load_WhenRoleOverride_ReadOnlyUsersIncludesNvlcrPmKhachHang()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""roles"": {
                    ""readOnlyUsers"": [""NVLCR"", ""PM"", ""KhachHang"", ""Customer""]
                }
            }";
            string path = Path.Combine(_tempDir, "roles_ro.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Contains("NVLCR", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("PM", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("KhachHang", result.Configuration.Roles.ReadOnlyUsers);
            Assert.Contains("Customer", result.Configuration.Roles.ReadOnlyUsers);
        }

        [Fact]
        public void Load_WhenEmptyIronCadPath_DefaultsSafely()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""local"": {
                    ""ironCadExecutablePath"": """"
                }
            }";
            string path = Path.Combine(_tempDir, "emptyironcad.json");
            File.WriteAllText(path, json);

            var result = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(result.IsValid);
            Assert.Empty(result.Configuration.Local.IronCadExecutablePath);
        }

        [Fact]
        public void Factory_FromFullValidConfig_MapsEveryField()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""environmentName"": ""UAT"",
                ""aras"": {
                    ""baseUrl"": ""https://aras.example.com/InnovatorServer"",
                    ""database"": ""MyDatabase"",
                    ""vaultId"": ""ABC123DEF456"",
                    ""oAuthClientId"": ""MyApp"",
                    ""oAuthScope"": ""MyScope"",
                    ""defaultMaxSearchResults"": 50,
                    ""timeoutSeconds"": 60
                },
                ""local"": {
                    ""ironCadExecutablePath"": ""C:\\IronCAD\\IRONCAD.exe""
                }
            }";
            string path = Path.Combine(_tempDir, "full.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.True(configResult.IsValid);
            Assert.Empty(configResult.Errors);

            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal("https://aras.example.com/InnovatorServer", options.BaseUri?.AbsoluteUri.TrimEnd('/'));
            Assert.Equal("MyDatabase", options.Database);
            Assert.Equal("ABC123DEF456", options.VaultId);
            Assert.Equal("MyApp", options.OAuthClientId);
            Assert.Equal("MyScope", options.OAuthScope);
            Assert.Equal(50, options.DefaultMaxSearchResults);
            Assert.Equal(60, (int)options.Timeout.TotalSeconds);
            Assert.Equal(@"C:\IronCAD\IRONCAD.exe", options.IronCadExecutablePath);
        }

        [Fact]
        public void Factory_FromMissingConfig_ReturnsSafeDefaults()
        {
            var configResult = new EnvironmentConfigurationResult
            {
                Configuration = new EnvironmentConfiguration(),
                SourcePath = "built-in defaults"
            };

            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.BaseUri);
            Assert.Null(options.Database);
            Assert.Null(options.VaultId);
        }

        [Fact]
        public void Factory_MissingBaseUri_ProducesError()
        {
            string json = @"{ ""schemaVersion"": 1, ""aras"": { ""database"": ""Db"" } }";
            string path = Path.Combine(_tempDir, "nobaseurl.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.BaseUri);
            Assert.Contains(configResult.Errors, e => e.Contains("baseUrl"));
        }

        [Fact]
        public void Factory_MissingDatabase_ProducesError()
        {
            string json = @"{ ""schemaVersion"": 1, ""aras"": { ""baseUrl"": ""https://example.com"" } }";
            string path = Path.Combine(_tempDir, "nodatabase.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.Database);
            Assert.Contains(configResult.Errors, e => e.Contains("database"));
        }

        [Fact]
        public void Factory_MissingVaultId_ProducesWarningNotError()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""database"": ""Db""
                }
            }";
            string path = Path.Combine(_tempDir, "novault.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            Assert.Empty(configResult.Errors);

            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.VaultId);
            Assert.Contains(configResult.Warnings, w => w.Contains("vaultId"));
        }

        [Fact]
        public void Factory_MalformedBaseUri_ProducesError()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""not a uri at all"",
                    ""database"": ""Db""
                }
            }";
            string path = Path.Combine(_tempDir, "baduri.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.BaseUri);
            Assert.Contains(configResult.Errors, e => e.Contains("valid absolute URI"));
        }

        [Fact]
        public void Factory_TimeoutSeconds_MapsCorrectly()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""database"": ""Db"",
                    ""timeoutSeconds"": 45
                }
            }";
            string path = Path.Combine(_tempDir, "timeout.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal(45, (int)options.Timeout.TotalSeconds);
        }

        [Fact]
        public void Factory_InvalidTimeoutSeconds_UsesDefaultAndWarns()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""database"": ""Db"",
                    ""timeoutSeconds"": -5
                }
            }";
            string path = Path.Combine(_tempDir, "badtimeout.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal(30, (int)options.Timeout.TotalSeconds);
            Assert.Contains(configResult.Warnings, w => w.Contains("timeoutSeconds"));
        }

        [Fact]
        public void Factory_DefaultMaxSearchResults_MapsCorrectly()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""database"": ""Db"",
                    ""defaultMaxSearchResults"": 100
                }
            }";
            string path = Path.Combine(_tempDir, "maxresults.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal(100, options.DefaultMaxSearchResults);
        }

        [Fact]
        public void Factory_InvalidDefaultMaxSearchResults_UsesDefaultAndWarns()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://example.com"",
                    ""database"": ""Db"",
                    ""defaultMaxSearchResults"": 0
                }
            }";
            string path = Path.Combine(_tempDir, "badmaxresults.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal(20, options.DefaultMaxSearchResults);
            Assert.Contains(configResult.Warnings, w => w.Contains("defaultMaxSearchResults"));
        }

        [Fact]
        public void Factory_IronCadExecutablePath_MapsCorrectly()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""local"": {
                    ""ironCadExecutablePath"": ""D:\\Tools\\IRONCAD.exe""
                }
            }";
            string path = Path.Combine(_tempDir, "ironcadpath.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Equal(@"D:\Tools\IRONCAD.exe", options.IronCadExecutablePath);
        }

        [Fact]
        public void Factory_WithLoginOverrides_PreservesNonLoginFields()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://original.com"",
                    ""database"": ""OriginalDb"",
                    ""vaultId"": ""VAULT-ORIG"",
                    ""oAuthClientId"": ""OrigApp"",
                    ""oAuthScope"": ""OrigScope"",
                    ""defaultMaxSearchResults"": 25,
                    ""timeoutSeconds"": 15
                },
                ""local"": {
                    ""ironCadExecutablePath"": ""C:\\orig\\ironcad.exe""
                }
            }";
            string path = Path.Combine(_tempDir, "override.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var original = ArasClientOptionsFactory.FromConfiguration(configResult);

            var overridden = original.WithLoginOverrides("https://login.com", "LoginDb");

            Assert.Equal("https://login.com/", overridden.BaseUri?.AbsoluteUri);
            Assert.Equal("LoginDb", overridden.Database);
            Assert.Equal("VAULT-ORIG", overridden.VaultId);
            Assert.Equal("OrigApp", overridden.OAuthClientId);
            Assert.Equal("OrigScope", overridden.OAuthScope);
            Assert.Equal(25, overridden.DefaultMaxSearchResults);
            Assert.Equal(15, (int)overridden.Timeout.TotalSeconds);
            Assert.Equal(@"C:\orig\ironcad.exe", overridden.IronCadExecutablePath);
        }

        [Fact]
        public void ArasClientOptions_Defaults_DoNotContainRealEnvironmentValues()
        {
            var options = new ArasClientOptions();

            Assert.Null(options.BaseUri);
            Assert.Null(options.Database);
            Assert.Null(options.VaultId);
            Assert.Null(options.IronCadExecutablePath);
        }

        [Fact]
        public void ResolvePath_EnvVarPrecedesSideBySideAndAppData()
        {
            string envVarPath = Path.Combine(_tempDir, "from-env-var.json");
            File.WriteAllText(envVarPath, @"{ ""schemaVersion"": 1, ""environmentName"": ""FromEnvVar"" }");

            string sideBySideDir = Path.Combine(_tempDir, "output");
            string appDataDir = Path.Combine(_tempDir, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""FromSideBySide"" }");
            File.WriteAllText(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""FromAppData"" }");

            var context = new EnvironmentConfigurationPathContext(envVarPath, sideBySideDir, appDataDir);
            string resolved = EnvironmentConfigurationLoader.ResolvePath(context, new List<string>());

            Assert.Equal(Path.GetFullPath(envVarPath), Path.GetFullPath(resolved));
            Assert.Equal("FromEnvVar", EnvironmentConfigurationLoader.Load(context).Configuration.EnvironmentName);
        }

        [Fact]
        public void Load_WithIsolatedCandidates_UsesExplicitEnvFileAndPreservesSentinel()
        {
            string root = Path.Combine(_tempDir, "isolated");
            string sideBySideDir = Path.Combine(root, "output");
            string appDataDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);

            string envPath = Path.Combine(root, "explicit.json");
            string sentinelPath = Path.Combine(appDataDir, "keep.txt");
            File.WriteAllText(envPath, @"{ ""schemaVersion"": 1, ""environmentName"": ""Explicit"" }");
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""SideBySide"" }");
            File.WriteAllText(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""AppData"" }");
            File.WriteAllText(sentinelPath, "must remain");

            var context = new EnvironmentConfigurationPathContext(envPath, sideBySideDir, appDataDir);
            var result = EnvironmentConfigurationLoader.Load(context);

            Assert.Equal("Explicit", result.Configuration.EnvironmentName);
            Assert.Equal("must remain", File.ReadAllText(sentinelPath));
            Assert.True(File.Exists(envPath));
        }

        [Fact]
        public void Load_WithMissingExplicitEnvFile_ReturnsErrorWithoutFallback()
        {
            string root = Path.Combine(_tempDir, "invalid-explicit");
            string sideBySideDir = Path.Combine(root, "output");
            string appDataDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""SideBySide"" }");
            File.WriteAllText(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""AppData"" }");

            var context = new EnvironmentConfigurationPathContext(
                Path.Combine(root, "missing.json"), sideBySideDir, appDataDir);
            var result = EnvironmentConfigurationLoader.Load(context);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("explicit environment config path"));
            Assert.NotEqual("SideBySide", result.Configuration.EnvironmentName);
            Assert.NotEqual("AppData", result.Configuration.EnvironmentName);
        }

        [Fact]
        public void Load_WithExplicitDirectoryPath_ReturnsErrorWithoutFallback()
        {
            string root = Path.Combine(_tempDir, "directory-explicit");
            string sideBySideDir = Path.Combine(root, "output");
            string appDataDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(Path.Combine(root, "config-directory"));
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""SideBySide"" }");

            var context = new EnvironmentConfigurationPathContext(
                Path.Combine(root, "config-directory"), sideBySideDir, appDataDir);
            var result = EnvironmentConfigurationLoader.Load(context);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("points to a directory"));
            Assert.NotEqual("SideBySide", result.Configuration.EnvironmentName);
        }

        [Fact]
        public void Load_WithMalformedExplicitEnvFile_ReturnsErrorWithoutFallback()
        {
            string root = Path.Combine(_tempDir, "malformed-explicit");
            string sideBySideDir = Path.Combine(root, "output");
            string appDataDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            string envPath = Path.Combine(root, "malformed.json");
            File.WriteAllText(envPath, "{ not valid json }");
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""SideBySide"" }");

            var context = new EnvironmentConfigurationPathContext(envPath, sideBySideDir, appDataDir);
            var result = EnvironmentConfigurationLoader.Load(context);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("malformed JSON"));
            Assert.NotEqual("SideBySide", result.Configuration.EnvironmentName);
        }

        [Fact]
        public void Load_WithBlankEnvValue_AllowsSideBySideFallback()
        {
            string root = Path.Combine(_tempDir, "blank-explicit");
            string sideBySideDir = Path.Combine(root, "output");
            string appDataDir = Path.Combine(root, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""SideBySide"" }");

            var context = new EnvironmentConfigurationPathContext("  ", sideBySideDir, appDataDir);
            var result = EnvironmentConfigurationLoader.Load(context);

            Assert.True(result.IsValid);
            Assert.Equal("SideBySide", result.Configuration.EnvironmentName);
        }

        [Fact]
        public void ResolvePath_SideBySidePrecedesAppData()
        {
            string sideBySideDir = Path.Combine(_tempDir, "output");
            string appDataDir = Path.Combine(_tempDir, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""FromSideBySide"" }");
            File.WriteAllText(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""FromAppData"" }");

            var context = new EnvironmentConfigurationPathContext(null, sideBySideDir, appDataDir);
            string resolved = EnvironmentConfigurationLoader.ResolvePath(context, new List<string>());

            Assert.Equal(Path.Combine(sideBySideDir, EnvironmentConfigurationLoader.FileName), resolved);
            Assert.Equal("FromSideBySide", EnvironmentConfigurationLoader.Load(context).Configuration.EnvironmentName);
        }

        [Fact]
        public void ResolvePath_AppDataFallback_WhenHigherAbsent()
        {
            string sideBySideDir = Path.Combine(_tempDir, "empty-output");
            string appDataDir = Path.Combine(_tempDir, "appdata");
            Directory.CreateDirectory(sideBySideDir);
            Directory.CreateDirectory(appDataDir);
            File.WriteAllText(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName),
                @"{ ""schemaVersion"": 1, ""environmentName"": ""FromAppData"" }");

            var context = new EnvironmentConfigurationPathContext(null, sideBySideDir, appDataDir);
            string resolved = EnvironmentConfigurationLoader.ResolvePath(context, new List<string>());

            Assert.Equal(Path.Combine(appDataDir, EnvironmentConfigurationLoader.FileName), resolved);
            Assert.Equal("FromAppData", EnvironmentConfigurationLoader.Load(context).Configuration.EnvironmentName);
        }

        [Fact]
        public void ResolvePath_NoFiles_ReturnsNull()
        {
            var context = new EnvironmentConfigurationPathContext(
                null, Path.Combine(_tempDir, "empty-output"), Path.Combine(_tempDir, "empty-appdata"));
            string resolved = EnvironmentConfigurationLoader.ResolvePath(context, new List<string>());

            Assert.Null(resolved);
            Assert.Equal("built-in defaults", EnvironmentConfigurationLoader.Load(context).SourcePath);
        }

        [Fact]
        public void Load_DirectoryPath_ReturnsNotFoundWarning()
        {
            string dirPath = Path.Combine(_tempDir, "subdir");
            Directory.CreateDirectory(dirPath);
            string dirAsFile = dirPath;

            var result = EnvironmentConfigurationLoader.LoadFromPath(dirAsFile);
            Assert.True(result.IsValid);
            Assert.NotEmpty(result.Warnings);
            Assert.Contains(result.Warnings, w => w.Contains("not found"));
        }

        [Fact]
        public void ArasClientOptions_Clone_ReturnsIndependentCopy()
        {
            var original = new ArasClientOptions
            {
                BaseUri = new Uri("https://original.com"),
                Database = "OriginalDb",
                VaultId = "Vault123",
                OAuthClientId = "MyApp",
                OAuthScope = "MyScope",
                Timeout = TimeSpan.FromSeconds(45),
                IronCadExecutablePath = @"C:\ironcad.exe",
                DefaultMaxSearchResults = 99
            };

            var clone = original.Clone();

            Assert.Equal("https://original.com/", clone.BaseUri?.AbsoluteUri);
            Assert.Equal("OriginalDb", clone.Database);
            Assert.Equal("Vault123", clone.VaultId);
            Assert.Equal(45, (int)clone.Timeout.TotalSeconds);

            clone.BaseUri = new Uri("https://modified.com");
            clone.Database = "ModifiedDb";
            Assert.Equal("https://original.com/", original.BaseUri?.AbsoluteUri);
            Assert.Equal("OriginalDb", original.Database);
        }

        [Fact]
        public void Factory_InitializeAndReset_StateCleared()
        {
            ArasClientOptionsFactory.Reset();
            // After Reset, internal state is cleared. Accessing CurrentConfig or
            // Current triggers lazy init, so we verify by checking that Initialize
            // succeeds and Reset can be called without error.
            Assert.False(ArasClientOptionsFactory.IsInitialized);

            ArasClientOptionsFactory.Initialize();
            Assert.True(ArasClientOptionsFactory.IsInitialized);
            Assert.NotNull(ArasClientOptionsFactory.CurrentConfig);
            Assert.NotNull(ArasClientOptionsFactory.Current);

            ArasClientOptionsFactory.Reset();
            Assert.False(ArasClientOptionsFactory.IsInitialized);
        }

        [Fact]
        public void Factory_Initialize_IsIdempotent()
        {
            ArasClientOptionsFactory.Reset();
            ArasClientOptionsFactory.Initialize();
            var firstConfig = ArasClientOptionsFactory.CurrentConfig;
            var firstOptions = ArasClientOptionsFactory.Current;

            ArasClientOptionsFactory.Initialize();
            var secondConfig = ArasClientOptionsFactory.CurrentConfig;
            var secondOptions = ArasClientOptionsFactory.Current;

            Assert.NotNull(firstConfig);
            Assert.NotNull(secondConfig);
            Assert.Equal(firstOptions?.BaseUri?.AbsoluteUri, secondOptions?.BaseUri?.AbsoluteUri);
            Assert.Equal(firstOptions?.Database, secondOptions?.Database);
        }

        [Fact]
        public void Factory_Current_ReturnsSnapshotNotSharedInstance()
        {
            ArasClientOptionsFactory.Reset();
            ArasClientOptionsFactory.Initialize();

            var first = ArasClientOptionsFactory.Current;
            var second = ArasClientOptionsFactory.Current;

            Assert.NotSame(first, second);
        }

        [Fact]
        public void VaultClient_MissingVaultId_UploadThrowsClearError()
        {
            var options = new ArasClientOptions
            {
                BaseUri = new Uri("https://example.com"),
                Database = "Db"
            };
            Assert.Null(options.VaultId);

            var http = new Aras.ArasHttpClient(options.BaseUri, TimeSpan.FromSeconds(5));
            var vault = new Aras.VaultClient(http, options);

            var ex = Assert.Throws<ArasOperationException>(() =>
                vault.UploadFileAsync("nonexistent.ics", "test.ics", CancellationToken.None)
                    .GetAwaiter().GetResult());

            Assert.Contains("Vault ID", ex.Message);
            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
        }

        [Fact]
        public void IronCadExternalAdapter_DefaultConstructor_HasNullPath()
        {
            string testFile = Path.Combine(_tempDir, "test.ics");
            File.WriteAllText(testFile, "dummy");
            var adapter = new IronCadExternalAdapter();
            var ex = Assert.Throws<FileNotFoundException>(() =>
                adapter.OpenDocumentAsync(testFile, CadOpenMode.ReadOnly, CancellationToken.None)
                    .GetAwaiter().GetResult());

            Assert.Contains("IronCAD executable", ex.Message);
        }

        [Fact]
        public void IronCadExternalAdapter_ExplicitNullPath_ThrowsOnOpen()
        {
            string testFile = Path.Combine(_tempDir, "test.ics");
            File.WriteAllText(testFile, "dummy");
            var adapter = new IronCadExternalAdapter(null);
            var ex = Assert.Throws<FileNotFoundException>(() =>
                adapter.OpenDocumentAsync(testFile, CadOpenMode.ReadOnly, CancellationToken.None)
                    .GetAwaiter().GetResult());

            Assert.Contains("IronCAD executable", ex.Message);
        }

        [Fact]
        public void IronCadOpenService_DefaultConstructor_NullPath_ReturnsFalse()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter);
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public void IronCadOpenService_ExplicitNullPath_ReturnsFalse()
        {
            var adapter = new StubCadAdapter();
            var service = new IronCadOpenService(adapter, null);
            Assert.False(service.IsIronCadAvailable);
        }

        [Fact]
        public void Factory_DesktopStartup_ObtainsConfiguredOptions()
        {
            string configPath = Path.Combine(_tempDir, "desktop-startup.json");
            File.WriteAllText(configPath, @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://desktop-test.example.com/InnovatorServer"",
                    ""database"": ""DesktopTestDb"",
                    ""vaultId"": ""DESKTOP-VAULT"",
                    ""timeoutSeconds"": 90
                },
                ""local"": {
                    ""ironCadExecutablePath"": ""D:\\desktop\\IRONCAD.exe""
                }
            }");

            string savedEnv = Environment.GetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName, configPath);

                ArasClientOptionsFactory.Reset();
                ArasClientOptionsFactory.Initialize();
                var options = ArasClientOptionsFactory.Current;

                Assert.Equal("https://desktop-test.example.com/InnovatorServer", options.BaseUri?.AbsoluteUri.TrimEnd('/'));
                Assert.Equal("DesktopTestDb", options.Database);
                Assert.Equal("DESKTOP-VAULT", options.VaultId);
                Assert.Equal(90, (int)options.Timeout.TotalSeconds);
                Assert.Equal(@"D:\desktop\IRONCAD.exe", options.IronCadExecutablePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName, savedEnv);
                ArasClientOptionsFactory.Reset();
            }
        }

        [Fact]
        public void Factory_IronCadAddinStartup_ObtainsConfiguredOptions()
        {
            string configPath = Path.Combine(_tempDir, "addin-startup.json");
            File.WriteAllText(configPath, @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": ""https://addin-test.example.com/InnovatorServer"",
                    ""database"": ""AddinTestDb"",
                    ""vaultId"": ""ADDIN-VAULT""
                },
                ""local"": {
                    ""ironCadExecutablePath"": ""E:\\addin\\IRONCAD.exe""
                }
            }");

            string savedEnv = Environment.GetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName);
            try
            {
                Environment.SetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName, configPath);

                ArasClientOptionsFactory.Reset();
                ArasClientOptionsFactory.Initialize();
                var options = ArasClientOptionsFactory.Current;

                Assert.Equal("https://addin-test.example.com/InnovatorServer", options.BaseUri?.AbsoluteUri.TrimEnd('/'));
                Assert.Equal("AddinTestDb", options.Database);
                Assert.Equal("ADDIN-VAULT", options.VaultId);
                Assert.Equal(@"E:\addin\IRONCAD.exe", options.IronCadExecutablePath);
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvironmentConfigurationLoader.EnvVarName, savedEnv);
                ArasClientOptionsFactory.Reset();
            }
        }

        [Fact]
        public void Factory_EmptyValuesDoNotEraseLowerPrecedence()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""aras"": {
                    ""baseUrl"": """",
                    ""database"": """",
                    ""vaultId"": """",
                    ""oAuthClientId"": """",
                    ""oAuthScope"": """"
                },
                ""local"": {
                    ""ironCadExecutablePath"": """"
                }
            }";
            string path = Path.Combine(_tempDir, "empty-values.json");
            File.WriteAllText(path, json);

            var configResult = EnvironmentConfigurationLoader.LoadFromPath(path);
            var options = ArasClientOptionsFactory.FromConfiguration(configResult);

            Assert.Null(options.BaseUri);
            Assert.Null(options.Database);
            Assert.Null(options.VaultId);
            Assert.Equal("IOMApp", options.OAuthClientId);
            Assert.Equal("Innovator", options.OAuthScope);
            Assert.Equal(20, options.DefaultMaxSearchResults);
            Assert.Equal(30, (int)options.Timeout.TotalSeconds);
            Assert.Null(options.IronCadExecutablePath);
        }

        [Fact]
        public void RealConfigFile_IsIgnored_ByGitIgnore()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", ".."));

            var gitignorePath = Path.Combine(repoRoot, ".gitignore");
            Assert.True(File.Exists(gitignorePath), ".gitignore not found at repo root");

            string gitignore = File.ReadAllText(gitignorePath);
            Assert.Contains("IdeaCadConnector.environment.json", gitignore);
        }

        internal sealed class StubCadAdapter : ICadApplicationAdapter
        {
            public string AuthoringTool => "Stub";
            public string AuthoringToolVersion => "1.0";
            public CadDocumentInfo GetActiveDocumentInfo() => null;
            public CadMetadata ReadMetadata() => new CadMetadata();
            public void WriteMetadata(CadMetadata metadata) { }
            public Task OpenDocumentAsync(string filePath, CadOpenMode openMode, CancellationToken ct) => Task.FromResult(0);
        }

        [Fact]
        public void TemplateFile_DoesNotContainRealInternalValues()
        {
            var templateDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..",
                "src", "IdeaCadConnector.Desktop");
            var templatePath = Path.Combine(
                Path.GetFullPath(templateDir),
                "IdeaCadConnector.environment.template.json");

            if (!File.Exists(templatePath))
            {
                templatePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "IdeaCadConnector.environment.template.json");
            }

            Assert.True(File.Exists(templatePath),
                $"Template not found at {templatePath}. Test may need path adjustment.");

            string content = File.ReadAllText(templatePath);

            Assert.DoesNotContain("172.16.10.227", content);
            Assert.DoesNotContain("InnovatorSolutions", content);
            Assert.DoesNotContain("67BBB9204FE84A8981ED8313049BA06C", content);
            Assert.DoesNotContain("IRONCAD.exe", content);
            Assert.DoesNotContain("IRONCAD\\2025", content);
        }
    }
}
