using System;
using System.IO;
using System.Linq;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Configuration;
using Xunit;

namespace IdeaCadConnector.Tests
{
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
