using System.Collections.Generic;
using IdeaCadConnector.Core.Dto;

namespace IdeaCadConnector.Desktop.Workflow
{
    public sealed class SubmitForReviewDialogResult
    {
        public bool Confirmed { get; set; }
        public string ChangeDescription { get; set; }
        public string SelectedReviewer { get; set; }
    }

    public sealed class ReviewDecisionDialogResult
    {
        public bool Confirmed { get; set; }
        public CadBusinessActionKind Kind { get; set; }
        public string Comment { get; set; }
    }

    public interface IWorkflowActionDialogService
    {
        SubmitForReviewDialogResult ShowSubmitForReview(
            string cadInfo, string partInfo, IEnumerable<string> reviewers);

        ReviewDecisionDialogResult ShowReviewDecision(
            string submissionInfo, string gateNote);

        bool ShowWithdrawConfirm(string submissionInfo);

        bool ShowGatePending(string title, string message);

        bool ShowReviewerUnavailable(string title, string message);

        bool ConfirmSimple(string title, string message);
    }
}
