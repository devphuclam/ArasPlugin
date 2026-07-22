using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class CadLifecyclePolicyTests
    {
        [Theory]
        [InlineData("Khoi tao", false)]
        [InlineData("Thiet ke chi tiet", false)]
        [InlineData("In Review", true)]
        [InlineData("Released", false)]
        [InlineData("In Change", false)]
        [InlineData("Superseded", false)]
        [InlineData("Loai bo", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void CanWithdraw_ReturnsExpected(string state, bool expected)
        {
            var result = CadLifecyclePolicy.CanWithdraw(state);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Khoi tao", false)]
        [InlineData("Thiet ke chi tiet", false)]
        [InlineData("In Review", false)]
        [InlineData("Released", true)]
        [InlineData(null, false)]
        public void IsStateReleased_ReturnsExpected(string state, bool expected)
        {
            var result = CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Khoi tao", false)]
        [InlineData("Thiet ke chi tiet", true)]
        [InlineData("In Review", false)]
        [InlineData("Released", false)]
        public void CanSubmitForReview_ReturnsExpected(string state, bool expected)
        {
            var result = CadLifecyclePolicy.CanSubmitForReview(state);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Khoi tao", false)]
        [InlineData("Thiet ke chi tiet", false)]
        [InlineData("In Review", true)]
        [InlineData("Released", false)]
        public void CanApproveReview_ReturnsExpected(string state, bool expected)
        {
            var result = CadLifecyclePolicy.CanApproveReview(state);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Khoi tao", false)]
        [InlineData("Thiet ke chi tiet", false)]
        [InlineData("In Review", true)]
        [InlineData("Released", false)]
        public void CanRequestRework_ReturnsExpected(string state, bool expected)
        {
            var result = CadLifecyclePolicy.CanRequestRework(state);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(CadBusinessActionKind.Withdraw, "Khoi tao", false)]
        [InlineData(CadBusinessActionKind.Withdraw, "Thiet ke chi tiet", false)]
        [InlineData(CadBusinessActionKind.Withdraw, "In Review", true)]
        [InlineData(CadBusinessActionKind.Withdraw, "Released", false)]
        [InlineData(CadBusinessActionKind.SubmitForReview, "Thiet ke chi tiet", true)]
        [InlineData(CadBusinessActionKind.SubmitForReview, "Khoi tao", false)]
        [InlineData(CadBusinessActionKind.Approve, "In Review", true)]
        [InlineData(CadBusinessActionKind.Approve, "Thiet ke chi tiet", false)]
        [InlineData(CadBusinessActionKind.RequestRework, "In Review", true)]
        [InlineData(CadBusinessActionKind.RequestRework, "Thiet ke chi tiet", false)]
        public void CanExecuteBusinessAction_WithdrawHandled(CadBusinessActionKind kind, string state, bool expected)
        {
            var result = CadLifecyclePolicy.CanExecuteBusinessAction(kind, state);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ICadLifecyclePolicy_Withdraw_DelegatesToStatic()
        {
            ICadLifecyclePolicy policy = new CadLifecyclePolicy();

            Assert.True(policy.CanWithdraw("In Review"));
            Assert.False(policy.CanWithdraw("Released"));
            Assert.False(policy.CanWithdraw(null));
        }

    }
}
