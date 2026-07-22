using System.Collections.Generic;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Desktop.Workflow
{
    public sealed class CheckinReasonDialogResult
    {
        public bool Confirmed { get; set; }
        public string Reason { get; set; }
    }

    public sealed class SubmitForReviewDialogResult
    {
        public bool Confirmed { get; set; }
        public string ChangeDescription { get; set; }
    }

    public sealed class ReviewDecisionDialogResult
    {
        public bool Confirmed { get; set; }
        public CadBusinessActionKind Kind { get; set; }
        public string Comment { get; set; }
    }

    public interface IWorkflowActionDialogService
    {
        CheckinReasonDialogResult ShowCheckinReason();

        SubmitForReviewDialogResult ShowSubmitForReview(
            string cadInfo, string partInfo);

        ReviewDecisionDialogResult ShowReviewDecision(
            string submissionInfo, string gateNote);

        bool ShowWithdrawConfirm(string submissionInfo);

        bool ShowGatePending(string title, string message);

        bool ShowReviewerUnavailable(string title, string message);

        bool ShowWorkflowActionError(string title, string message);

        bool ConfirmSimple(string title, string message);
    }
}
