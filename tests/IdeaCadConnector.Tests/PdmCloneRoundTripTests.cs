using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public class PdmCloneRoundTripTests
    {
        [Fact]
        public void CloneClient_AcceptsInjectedVaultDownloader()
        {
            var aml = new CloneAmlClient();
            var vault = new CloneVaultClient();
            var options = new ArasClientOptions { BaseUri = new Uri("http://fake/"), Database = "db" };

            using var client = new HttpPdmRepositoryClient(
                options, aml, vault, NullLogger<HttpPdmRepositoryClient>.Instance);

            Assert.NotNull(client);
        }

        private sealed class CloneAmlClient : IArasAmlClient
        {
            public Task<JObject> ApplyMethodAsync(
                string methodName,
                IDictionary<string, string> parameters,
                CancellationToken ct)
            {
                return Task.FromResult(new JObject());
            }

            public Task<JObject> ApplyItemAsync(
                string itemType,
                string itemId,
                string action,
                string selectFields,
                CancellationToken ct)
            {
                return Task.FromResult(new JObject());
            }

            public Task<JObject> ApplyAmlAsync(
                string amlBody,
                string action,
                string itemType,
                string itemId,
                CancellationToken ct)
            {
                return Task.FromResult(new JObject());
            }
        }

        private sealed class CloneVaultClient : IVaultFileClient
        {
            public Task<string> UploadFileAsync(
                string filePath,
                string fileName,
                CancellationToken ct)
            {
                throw new NotSupportedException();
            }

            public Task<string> DownloadFileAsync(
                string fileId,
                string targetDirectory,
                CancellationToken ct)
            {
                return Task.FromResult<string>(null);
            }
        }
    }
}
