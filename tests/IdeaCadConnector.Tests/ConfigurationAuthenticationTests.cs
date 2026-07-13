using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Configuration;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class ConfigurationAuthenticationTests
    {
        [Fact]
        public void Factory_NormalizesBaseUriToExactlyOneTrailingSlash()
        {
            using var configFile = CreateConfigPath(
                @"{ ""schemaVersion"": 1, ""aras"": { ""baseUrl"": ""https://host/InnovatorServer///"", ""database"": ""TestDb"" } }");
            var result = EnvironmentConfigurationLoader.LoadFromPath(configFile.Path);

            var options = ArasClientOptionsFactory.FromConfiguration(result);

            Assert.Equal("https://host/InnovatorServer/", options.BaseUri.AbsoluteUri);
        }

        [Theory]
        [InlineData("relative/path")]
        [InlineData("file:///C:/config")]
        [InlineData("ftp://host/InnovatorServer")]
        public void Factory_RejectsNonHttpAbsoluteBaseUri(string baseUrl)
        {
            using var configFile = CreateConfigPath(
                "{ \"schemaVersion\": 1, \"aras\": { \"baseUrl\": \"" + baseUrl + "\", \"database\": \"TestDb\" } }");
            var result = EnvironmentConfigurationLoader.LoadFromPath(configFile.Path);

            var options = ArasClientOptionsFactory.FromConfiguration(result);

            Assert.Null(options.BaseUri);
            Assert.Contains(result.Errors, error => error.Contains("http or https"));
        }

        [Fact]
        public void LoginOverrides_NormalizeBaseUriAndPreserveOAuthValues()
        {
            var options = new ArasClientOptions
            {
                BaseUri = new Uri("https://original.example/InnovatorServer/"),
                Database = "OriginalDb",
                OAuthClientId = "ConfiguredClient",
                OAuthScope = "ConfiguredScope"
            };

            var overridden = options.WithLoginOverrides(
                "https://login.example/InnovatorServer///", "LoginDb");

            Assert.Equal("https://login.example/InnovatorServer/", overridden.BaseUri.AbsoluteUri);
            Assert.Equal("ConfiguredClient", overridden.OAuthClientId);
            Assert.Equal("ConfiguredScope", overridden.OAuthScope);
        }

        [Fact]
        public void HttpClientLogin_UsesConfiguredOAuthAndResolvesTokenEndpoint()
        {
            using var configFile = CreateConfigPath(
                @"{
                    ""schemaVersion"": 1,
                    ""aras"": {
                        ""baseUrl"": ""https://host/InnovatorServer///"",
                        ""database"": ""TestDb"",
                        ""oauthClientId"": ""ConfiguredClient"",
                        ""oauthScope"": ""ConfiguredScope""
                    }
                }");
            var config = EnvironmentConfigurationLoader.LoadFromPath(configFile.Path);
            var options = ArasClientOptionsFactory.FromConfiguration(config);
            var handler = new CapturingHandler();
            using var client = new HttpArasCadClient(options, null, null, handler);

            var result = client.LoginAsync(
                new ArasLoginRequest { UserName = "user", Password = "password", Database = "TestDb" },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("user", result.UserName);
            Assert.Equal("https://host/InnovatorServer/oauthserver/connect/token", handler.TokenRequestUri);
            Assert.Contains("client_id=ConfiguredClient", handler.TokenForm);
            Assert.Contains("scope=ConfiguredScope", handler.TokenForm);
            Assert.DoesNotContain("IOMApp", handler.TokenForm);
            Assert.DoesNotContain("Innovator", handler.TokenForm);
        }

        [Fact]
        public void HttpClientLogin_MissingOAuthValuesReturnsValidationError()
        {
            var options = new ArasClientOptions
            {
                BaseUri = new Uri("https://host/InnovatorServer/"),
                Database = "TestDb",
                OAuthClientId = "",
                OAuthScope = ""
            };
            using var client = new HttpArasCadClient(options, null, null, new CapturingHandler());

            var ex = Assert.Throws<ArasOperationException>(() => client.LoginAsync(
                new ArasLoginRequest { UserName = "user", Password = "password", Database = "TestDb" },
                CancellationToken.None).GetAwaiter().GetResult());

            Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            Assert.Contains("OAuth", ex.Message);
        }

        [Fact]
        public void VaultUpload_ResolvesEndpointUnderInnovatorServerPath()
        {
            string filePath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "IdeaCadConnector_VaultTest_" + Guid.NewGuid().ToString("N") + ".ics");
            File.WriteAllText(filePath, "test");
            try
            {
                var options = new ArasClientOptions
                {
                    BaseUri = new Uri("https://host/InnovatorServer/"),
                    Database = "TestDb",
                    VaultId = "vault-id"
                };
                var handler = new CapturingHandler();
                using var http = new ArasHttpClient(options.BaseUri, TimeSpan.FromSeconds(5), handler);
                var vault = new VaultClient(http, options);

                vault.UploadFileAsync(filePath, "test.ics", CancellationToken.None)
                    .GetAwaiter().GetResult();

                Assert.Equal("https://host/InnovatorServer/vault/vaultserver.aspx", handler.LastRequestUri);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        private static TempConfigFile CreateConfigPath(string json)
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "IdeaCadConnector_AuthConfig_" + Guid.NewGuid().ToString("N") + ".json");
            System.IO.File.WriteAllText(path, json);
            return new TempConfigFile(path);
        }

        private sealed class TempConfigFile : IDisposable
        {
            public TempConfigFile(string path) { Path = path; }
            public string Path { get; }
            public void Dispose()
            {
                if (System.IO.File.Exists(Path))
                    System.IO.File.Delete(Path);
            }
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string TokenRequestUri { get; private set; }
            public string TokenForm { get; private set; }
            public string LastRequestUri { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string form = request.Content == null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                LastRequestUri = request.RequestUri.AbsoluteUri;

                bool isTokenRequest = request.RequestUri.AbsolutePath.EndsWith(
                    "/oauthserver/connect/token", StringComparison.OrdinalIgnoreCase);
                if (isTokenRequest)
                {
                    TokenRequestUri = request.RequestUri.AbsoluteUri;
                    TokenForm = form;
                }

                string body = isTokenRequest
                    ? "{\"access_token\":\"token\",\"token_type\":\"Bearer\"}"
                    : "{}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body)
                };
            }
        }
    }
}
