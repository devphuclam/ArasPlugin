using System;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class HttpArasCadClientTests
    {
        private static readonly CadOperationContext FakeContext = new CadOperationContext(
            "CAD1", "CAD-001", "A", 1, "In Review", "2026-07-20",
            true, false, null, null, null, null);

        [Fact]
        public async Task Withdraw_ThrowsWorkflowActionNotAvailable_WhileGateWClosed()
        {
            using (var client = new HttpArasCadClient(new ArasClientOptions(), NullLogger<HttpArasCadClient>.Instance))
            {
                client.OperationContextProvider = (id, ct) => Task.FromResult(FakeContext);

                var request = new ExecuteCadBusinessActionRequest(
                    "CAD1", CadBusinessActionKind.Withdraw, null, null, null, null);

                var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                    client.ExecuteCadBusinessActionAsync(request, CancellationToken.None));

                Assert.Equal(ArasErrorCode.WorkflowActionNotAvailable, ex.ErrorCode);
                Assert.Contains("GATE-W", ex.Message);
            }
        }

        [Fact]
        public async Task Withdraw_ThrowsBeforeServerMethodCall()
        {
            using (var client = new HttpArasCadClient(new ArasClientOptions(), NullLogger<HttpArasCadClient>.Instance))
            {
                client.OperationContextProvider = (id, ct) => Task.FromResult(FakeContext);

                var request = new ExecuteCadBusinessActionRequest(
                    "CAD1", CadBusinessActionKind.Withdraw, null, null, null, null);

                await Assert.ThrowsAsync<ArasOperationException>(() =>
                    client.ExecuteCadBusinessActionAsync(request, CancellationToken.None));
            }
        }
    }
}
