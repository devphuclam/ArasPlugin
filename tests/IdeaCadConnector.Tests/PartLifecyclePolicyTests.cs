using IdeaCadConnector.Core.Library;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PartLifecyclePolicyTests
    {
        private readonly PartLifecyclePolicy _policy = new PartLifecyclePolicy();

        [Theory]
        [InlineData("Khoi tao")]
        [InlineData("Thiet ke chi tiet")]
        [InlineData("Released")]
        public void CanRelease_OnlyAllowsInReview(string state)
        {
            Assert.False(_policy.CanRelease(state));
        }

        [Fact]
        public void CanRelease_AllowsInReview()
        {
            Assert.True(_policy.CanRelease("In Review"));
            Assert.True(_policy.CanRelease(" in review "));
        }

        [Theory]
        [InlineData("Released", true)]
        [InlineData("released", true)]
        [InlineData("In Review", false)]
        [InlineData("Thiet ke chi tiet", false)]
        [InlineData(null, false)]
        [InlineData("", false)]
        public void IsReleased_UsesOnlyMvpReleasedState(string state, bool expected)
        {
            Assert.Equal(expected, _policy.IsReleased(state));
        }

        [Fact]
        public void PolicyDoesNotReuseCadStateConstants()
        {
            Assert.Equal("Released", PartLifecyclePolicy.Released);
            Assert.NotEqual("Loai bo", PartLifecyclePolicy.Released);
        }

        [Fact]
        public void LegacyPartLibraryHelpersRemainAvailable()
        {
            Assert.True(PartLifecyclePolicy.IsPartObsolete("Obsolete"));
            Assert.False(PartLifecyclePolicy.IsReusable("Obsolete"));
            Assert.Null(PartLifecyclePolicy.GetPartNotReusableMessage("Released", "P-001"));
        }
    }
}
