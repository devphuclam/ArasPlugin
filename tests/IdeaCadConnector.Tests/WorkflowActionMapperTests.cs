using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class WorkflowActionMapperTests
    {
        [Fact]
        public void DefaultMapper_MapsWithdrawActivity_ToWithdraw()
        {
            var mapper = WorkflowActionMapper.CreateDefault();

            var result = mapper.Map("Withdraw", "");

            Assert.Equal(CadBusinessActionKind.Withdraw, result);
        }

        [Fact]
        public void DefaultMapper_MapsGenericInReviewActivity_ToSubmitForReview()
        {
            var mapper = WorkflowActionMapper.CreateDefault();

            var result = mapper.Map("Auto To In Review", "");

            Assert.Equal(CadBusinessActionKind.SubmitForReview, result);
        }

        [Fact]
        public void DefaultMapper_DoesNotContainHardCodedReviewerIdentity()
        {
            var mapper = WorkflowActionMapper.CreateDefault();

            Assert.Null(mapper.Map("ExampleReviewer_Review", "Approve"));
            Assert.Null(mapper.Map("ExampleReviewer_Review", "Reject"));
            Assert.Null(mapper.Map("ExampleContributor_Submit", ""));
        }

        [Fact]
        public void DefaultMapper_ReturnsNull_ForUnknownActivity()
        {
            var mapper = WorkflowActionMapper.CreateDefault();

            Assert.Null(mapper.Map("SomeUnknownActivity", "Approve"));
        }
    }
}
