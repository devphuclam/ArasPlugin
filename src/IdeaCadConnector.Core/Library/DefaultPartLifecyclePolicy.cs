namespace IdeaCadConnector.Core.Library
{
    /// <summary>
    /// Default client-side Part release guard. Mirrors the MVP Part lifecycle
    /// (Khoi tao -> Thiet ke chi tiet -> In Review -> Released). The PDM
    /// authority remains the source of truth for transitions.
    /// </summary>
    public sealed class DefaultPartLifecyclePolicy : IPartLifecyclePolicy
    {
        private readonly PartLifecyclePolicy _policy = new PartLifecyclePolicy();

        public bool CanRelease(string state)
        {
            return _policy.CanRelease(state);
        }

        public bool IsReleased(string state)
        {
            return _policy.IsReleased(state);
        }
    }
}
