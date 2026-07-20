using System;
using IdeaCadConnector.Core.Dto;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class CadBusinessActionKindTests
    {
        [Fact]
        public void Enum_ContainsAllExpectedValues()
        {
            var values = (CadBusinessActionKind[])Enum.GetValues(typeof(CadBusinessActionKind));
            Assert.Contains(CadBusinessActionKind.Checkout, values);
            Assert.Contains(CadBusinessActionKind.OpenReadOnly, values);
            Assert.Contains(CadBusinessActionKind.Checkin, values);
            Assert.Contains(CadBusinessActionKind.CancelCheckout, values);
            Assert.Contains(CadBusinessActionKind.StartDetailedDesign, values);
            Assert.Contains(CadBusinessActionKind.SubmitForReview, values);
            Assert.Contains(CadBusinessActionKind.Approve, values);
            Assert.Contains(CadBusinessActionKind.RequestRework, values);
            Assert.Contains(CadBusinessActionKind.Withdraw, values);
        }

        [Fact]
        public void Enum_HasNineValues()
        {
            var values = (CadBusinessActionKind[])Enum.GetValues(typeof(CadBusinessActionKind));
            Assert.Equal(9, values.Length);
        }

        [Fact]
        public void Withdraw_IsPresent()
        {
            Assert.True(Enum.IsDefined(typeof(CadBusinessActionKind), "Withdraw"));
        }
    }
}
