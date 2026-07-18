using IdeaCadConnector.Core.Cad;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Aras
{
    public sealed class ArasCadLifecycleAdapter : ICadLifecyclePolicy
    {
        public bool CanCheckout(string state)
            => CadLifecyclePolicy.CanCheckout(state);

        public bool CanSubmitForReview(string state)
            => CadLifecyclePolicy.CanSubmitForReview(state);

        public bool CanApprove(string state)
            => CadLifecyclePolicy.CanApproveReview(state);

        public bool CanRequestRework(string state)
            => CadLifecyclePolicy.CanRequestRework(state);

        public bool CanWithdraw(string state)
            => !string.IsNullOrWhiteSpace(state)
                && string.Equals(
                    state.Trim(),
                    CadLifecyclePolicy.InReview,
                    System.StringComparison.OrdinalIgnoreCase);

        public bool IsReleased(string state)
            => CadLifecyclePolicy.IsState(state, CadLifecyclePolicy.Released);

        public bool CanExecuteBusinessAction(CadBusinessActionKind kind, string state)
            => CadLifecyclePolicy.CanExecuteBusinessAction(kind, state);
    }
}
