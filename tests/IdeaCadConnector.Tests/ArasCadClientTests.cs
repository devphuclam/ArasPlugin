using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Aras;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class ArasCadClientTests
    {
        private static readonly CadOperationContext FakeContext = new CadOperationContext(
            "CAD1", "CAD-001", "A", 1, "In Review", "2026-07-20",
            true, false, null, null, null, (IReadOnlyList<CadBusinessAction>)null);

        [Fact]
        public async Task Withdraw_ThrowsWorkflowActionNotAvailable_WhileGateWClosed()
        {
            using (var client = new ArasCadClient(new ArasClientOptions(), NullLogger<ArasCadClient>.Instance))
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
        public async Task Withdraw_ValidatesCadId_BeforeSwitch()
        {
            using (var client = new ArasCadClient(new ArasClientOptions(), NullLogger<ArasCadClient>.Instance))
            {
                client.OperationContextProvider = (id, ct) => Task.FromResult(FakeContext);

                var request = new ExecuteCadBusinessActionRequest(
                    "", CadBusinessActionKind.Withdraw, null, null, null, null);

                var ex = await Assert.ThrowsAsync<ArasOperationException>(() =>
                    client.ExecuteCadBusinessActionAsync(request, CancellationToken.None));

                Assert.Equal(ArasErrorCode.ValidationFailed, ex.ErrorCode);
            }
        }

        [Fact]
        public async Task Withdraw_ThrowsBeforeServerMethodCall()
        {
            using (var client = new ArasCadClient(new ArasClientOptions(), NullLogger<ArasCadClient>.Instance))
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
