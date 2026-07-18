using IdeaCadConnector.Core.Library;

namespace IdeaCadConnector.Aras
{
    public sealed class ArasPartLifecycleAdapter : IPartLifecyclePolicy
    {
        private readonly string _releasableState;
        private readonly string _releasedState;

        public ArasPartLifecycleAdapter(
            string releasableState,
            string releasedState)
        {
            _releasableState = releasableState;
            _releasedState = releasedState;
        }

        public bool CanRelease(string state)
            => !string.IsNullOrWhiteSpace(state)
                && string.Equals(
                    state.Trim(),
                    _releasableState,
                    System.StringComparison.OrdinalIgnoreCase);

        public bool IsReleased(string state)
            => !string.IsNullOrWhiteSpace(state)
                && string.Equals(
                    state.Trim(),
                    _releasedState,
                    System.StringComparison.OrdinalIgnoreCase);
    }
}
