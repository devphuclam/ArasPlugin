using System.Collections.Generic;
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

        [Fact]
        public void DefaultMapper_InfersSubmitForSingleOpenPath_InDetailedDesign()
        {
            var paths = new List<CadWorkflowPath>
            {
                new CadWorkflowPath("path-1", "Continue", false)
            };

            var result = WorkflowActionMapper.InferSingleOpenPathAction(
                CadLifecyclePolicy.DetailedDesign,
                paths);

            Assert.Equal(CadBusinessActionKind.SubmitForReview, result);
        }

        [Fact]
        public void DefaultMapper_DoesNotInferSubmit_WhenThereIsNotExactlyOneOpenPath()
        {
            var paths = new List<CadWorkflowPath>
            {
                new CadWorkflowPath("path-1", "Continue", false),
                new CadWorkflowPath("path-2", "Other", false)
            };

            var result = WorkflowActionMapper.InferSingleOpenPathAction(
                CadLifecyclePolicy.DetailedDesign,
                paths);

            Assert.Null(result);
        }

        [Fact]
        public void DefaultMapper_DoesNotInferSubmit_OutsideDetailedDesign()
        {
            var paths = new List<CadWorkflowPath>
            {
                new CadWorkflowPath("path-1", "Continue", false)
            };

            var result = WorkflowActionMapper.InferSingleOpenPathAction(
                CadLifecyclePolicy.InReview,
                paths);

            Assert.Null(result);
        }

        [Fact]
        public void SubmitForReview_UsesWorkflowAssignment_WhenAssignmentAndPathArePresent()
        {
            Assert.True(WorkflowActionExecutionPolicy.UsesWorkflowAssignment(
                CadBusinessActionKind.SubmitForReview,
                "assignment-1",
                "path-1"));
        }

        [Theory]
        [InlineData(CadBusinessActionKind.StartDetailedDesign, "assignment-1", "path-1")]
        [InlineData(CadBusinessActionKind.SubmitForReview, "", "path-1")]
        [InlineData(CadBusinessActionKind.SubmitForReview, "assignment-1", "")]
        public void SubmitForReview_DoesNotUseWorkflowAssignment_WhenSelectionIsIncomplete(
            CadBusinessActionKind action,
            string assignmentId,
            string pathId)
        {
            Assert.False(WorkflowActionExecutionPolicy.UsesWorkflowAssignment(
                action,
                assignmentId,
                pathId));
        }
    }
}
