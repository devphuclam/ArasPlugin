using System.Threading;
using System.Threading.Tasks;
using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Library;
using IdeaCadConnector.Core.Policies;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class MvpReleaseEligibilityTests
    {
        private static MvpReleaseEligibility Build()
        {
            var gate = new CadWorkflowGate();
            gate.OpenPartReleaseGate();
            return new MvpReleaseEligibility(new CadLifecyclePolicy(), new DefaultPartLifecyclePolicy(), gate);
        }

        [Fact]
        public async Task InReviewCad_And_InReviewPart_IsEligible()
        {
            var eligibility = Build();
            var snapshot = new CadReleaseEligibilitySnapshot
            {
                CadId = "CAD1",
                PartId = "PART1",
                CadState = "In Review",
                PartState = "In Review"
            };

            var result = await eligibility.CheckAsync(snapshot, CancellationToken.None);

            Assert.True(result.IsEligible);
        }

        [Fact]
        public async Task DetailedDesignCad_IsNotEligible()
        {
            var eligibility = Build();
            var snapshot = new CadReleaseEligibilitySnapshot
            {
                CadId = "CAD1",
                PartId = "PART1",
                CadState = "Thiet ke chi tiet",
                PartState = "In Review"
            };

            var result = await eligibility.CheckAsync(snapshot, CancellationToken.None);

            Assert.False(result.IsEligible);
        }

        [Fact]
        public async Task ReleasedPart_IsNotEligible()
        {
            var eligibility = Build();
            var snapshot = new CadReleaseEligibilitySnapshot
            {
                CadId = "CAD1",
                PartId = "PART1",
                CadState = "In Review",
                PartState = "Released"
            };

            var result = await eligibility.CheckAsync(snapshot, CancellationToken.None);

            Assert.False(result.IsEligible);
        }

        [Fact]
        public async Task NullSnapshot_IsNotEligible()
        {
            var eligibility = Build();

            var result = await eligibility.CheckAsync(null, CancellationToken.None);

            Assert.False(result.IsEligible);
        }

        [Fact]
        public async Task GateAClosed_BlocksPartRelease_RegardlessOfState()
        {
            var gate = new CadWorkflowGate(); // GATE-A not opened
            var eligibility = new MvpReleaseEligibility(
                new CadLifecyclePolicy(), new DefaultPartLifecyclePolicy(), gate);

            var snapshot = new CadReleaseEligibilitySnapshot
            {
                CadId = "CAD1",
                PartId = "PART1",
                CadState = "In Review",
                PartState = "In Review"
            };

            var result = await eligibility.CheckAsync(snapshot, CancellationToken.None);

            Assert.False(result.IsEligible);
            Assert.Contains("GATE-A", string.Join("; ", result.BlockingReasons));
        }
    }
}
