using IdeaCadConnector.Desktop;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmCadLaunchActionStateTests
    {
        [Fact]
        public void Create_NoSelection_IsHidden()
        {
            var state = PdmCadLaunchActionState.Create(new PdmCadLaunchActionContext());

            Assert.Equal(PdmCadLaunchMode.Hidden, state.Mode);
            Assert.False(state.IsVisible);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void Create_RootOrMissingPrimaryCad_IsHidden(bool isRoot, bool hasPrimaryCad)
        {
            var context = ReadyContext();
            context.IsRootAssembly = isRoot;
            context.HasPrimaryCad = hasPrimaryCad;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.Equal(PdmCadLaunchMode.Hidden, state.Mode);
        }

        [Fact]
        public void Create_Disconnected_RemainsVisibleButDisabled()
        {
            var context = ReadyContext();
            context.IsConnected = false;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.Equal(PdmCadLaunchMode.Unavailable, state.Mode);
            Assert.True(state.IsVisible);
            Assert.False(state.IsEnabled);
            Assert.Equal("PdmCadLaunchConnectToAras", state.DisabledReasonKey);
        }

        [Theory]
        [InlineData(false, true, "PdmCadLaunchRefreshCad")]
        [InlineData(true, false, "PdmCadLaunchRefreshState")]
        public void Create_MissingLivePrerequisite_IsVisibleAndDisabled(
            bool hasLiveCadId,
            bool hasLifecycleState,
            string reasonKey)
        {
            var context = ReadyContext();
            context.HasLiveCadId = hasLiveCadId;
            context.HasLifecycleState = hasLifecycleState;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.True(state.IsVisible);
            Assert.False(state.IsEnabled);
            Assert.Equal(reasonKey, state.DisabledReasonKey);
        }

        [Fact]
        public void Create_EditableCad_IsCheckoutAndOpen()
        {
            var state = PdmCadLaunchActionState.Create(ReadyContext());

            Assert.Equal(PdmCadLaunchMode.CheckoutAndOpen, state.Mode);
            Assert.True(state.IsEnabled);
            Assert.Equal("PdmCheckoutAndOpenIronCad", state.LabelKey);
        }

        [Fact]
        public void Create_ValidLocalCheckout_IsOpenCheckedOut()
        {
            var context = ReadyContext();
            context.HasValidLocalCheckout = true;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.Equal(PdmCadLaunchMode.OpenCheckedOut, state.Mode);
            Assert.True(state.IsEnabled);
            Assert.Equal("PdmOpenCheckedOutIronCad", state.LabelKey);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void Create_NonEditableOrLockedCadWithNative_IsReadOnly(bool canCheckout, bool lockedByOther)
        {
            var context = ReadyContext();
            context.CanCheckout = canCheckout;
            context.IsLockedByOther = lockedByOther;
            context.HasNativeFile = true;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.Equal(PdmCadLaunchMode.OpenReadOnly, state.Mode);
            Assert.True(state.IsEnabled);
            Assert.Equal("PdmOpenIronCadReadOnly", state.LabelKey);
        }

        [Fact]
        public void Create_LockedCadWithoutNative_IsVisibleAndDisabled()
        {
            var context = ReadyContext();
            context.IsLockedByOther = true;
            context.HasNativeFile = false;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.Equal(PdmCadLaunchMode.Unavailable, state.Mode);
            Assert.True(state.IsVisible);
            Assert.False(state.IsEnabled);
            Assert.Equal("PdmCadLaunchNoReadableFile", state.DisabledReasonKey);
        }

        [Fact]
        public void Create_Busy_IsVisibleAndDisabled()
        {
            var context = ReadyContext();
            context.IsBusy = true;

            var state = PdmCadLaunchActionState.Create(context);

            Assert.True(state.IsVisible);
            Assert.False(state.IsEnabled);
            Assert.Equal("PdmCadLaunchBusy", state.DisabledReasonKey);
        }

        private static PdmCadLaunchActionContext ReadyContext()
        {
            return new PdmCadLaunchActionContext
            {
                HasSelection = true,
                HasPrimaryCad = true,
                IsConnected = true,
                HasLiveCadId = true,
                HasLifecycleState = true,
                CanCheckout = true,
                HasNativeFile = true
            };
        }
    }
}
