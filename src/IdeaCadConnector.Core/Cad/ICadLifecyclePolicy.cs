using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Core.Cad
{
    public interface ICadLifecyclePolicy
    {
        bool CanCheckout(string state);
        bool CanSubmitForReview(string state);
        bool CanApprove(string state);
        bool CanRequestRework(string state);
        bool CanWithdraw(string state);
        bool IsReleased(string state);
        bool CanExecuteBusinessAction(CadBusinessActionKind kind, string state);
    }
}
