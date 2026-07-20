using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class CadWorkflowGateTests
    {
        [Fact]
        public void Default_NonGatedActions_AreAvailable()
        {
            var gate = new CadWorkflowGate();

            // StartDetailedDesign is available as soon as the live CAD lifecycle
            // allows it. SubmitForReview still requires verified reviewer
            // assignment behavior.
            Assert.False(gate.IsReviewerAssignmentAvailable());
            Assert.True(gate.IsAvailable(CadBusinessActionKind.StartDetailedDesign));
        }

        [Fact]
        public void Default_GatedActions_AreNotAvailable()
        {
            var gate = new CadWorkflowGate();

            Assert.False(gate.IsAvailable(CadBusinessActionKind.Approve));
            Assert.False(gate.IsAvailable(CadBusinessActionKind.RequestRework));
            Assert.False(gate.IsAvailable(CadBusinessActionKind.Withdraw));
        }

        [Fact]
        public void IsGated_DistinguishesGatedFromNonGated()
        {
            Assert.False(CadWorkflowGate.IsGated(CadBusinessActionKind.SubmitForReview));
            Assert.False(CadWorkflowGate.IsGated(CadBusinessActionKind.StartDetailedDesign));
            Assert.True(CadWorkflowGate.IsGated(CadBusinessActionKind.Approve));
            Assert.True(CadWorkflowGate.IsGated(CadBusinessActionKind.RequestRework));
            Assert.True(CadWorkflowGate.IsGated(CadBusinessActionKind.Withdraw));
        }

        [Fact]
        public void OpenGate_MakesGatedActionAvailable()
        {
            var gate = new CadWorkflowGate();

            gate.OpenGate(CadBusinessActionKind.Withdraw);

            Assert.True(gate.IsAvailable(CadBusinessActionKind.Withdraw));
            Assert.False(gate.IsAvailable(CadBusinessActionKind.Approve));
        }

        [Fact]
        public void CloseGate_HidesActionAgain()
        {
            var gate = new CadWorkflowGate();
            gate.OpenGate(CadBusinessActionKind.Approve);

            gate.CloseGate(CadBusinessActionKind.Approve);

            Assert.False(gate.IsAvailable(CadBusinessActionKind.Approve));
        }

        [Fact]
        public void PartReleaseGate_ClosedByDefault()
        {
            var gate = new CadWorkflowGate();

            Assert.False(gate.IsPartReleaseAvailable());

            gate.OpenPartReleaseGate();

            Assert.True(gate.IsPartReleaseAvailable());

            gate.ClosePartReleaseGate();

            Assert.False(gate.IsPartReleaseAvailable());
        }

        [Fact]
        public void StartNewRevisionGate_ClosedByDefault()
        {
            var gate = new CadWorkflowGate();

            Assert.False(gate.IsStartNewRevisionAvailable());

            gate.OpenStartNewRevisionGate();

            Assert.True(gate.IsStartNewRevisionAvailable());

            gate.CloseStartNewRevisionGate();

            Assert.False(gate.IsStartNewRevisionAvailable());
        }

        [Fact]
        public void ReviewerAssignmentGate_ClosedByDefault()
        {
            var gate = new CadWorkflowGate();

            Assert.False(gate.IsReviewerAssignmentAvailable());

            gate.OpenReviewerAssignmentGate();
            Assert.True(gate.IsReviewerAssignmentAvailable());

            gate.CloseReviewerAssignmentGate();
            Assert.False(gate.IsReviewerAssignmentAvailable());
        }
    }
}
