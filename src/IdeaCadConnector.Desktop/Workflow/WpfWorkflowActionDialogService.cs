using System.Collections.Generic;
using System.Linq;
using System.Windows;
using IdeaCadConnector.Core.Dto;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop.Workflow
{
    public sealed class WpfWorkflowActionDialogService : IWorkflowActionDialogService
    {
        public SubmitForReviewDialogResult ShowSubmitForReview(
            string cadInfo, string partInfo, IEnumerable<string> reviewers)
        {
            var dialog = new SubmitForReviewDialog();
            if (!string.IsNullOrWhiteSpace(cadInfo)) dialog.SetCadInfo(cadInfo);
            if (!string.IsNullOrWhiteSpace(partInfo)) dialog.SetPartInfo(partInfo);
            if (reviewers != null) dialog.SetReviewers(reviewers);

            var result = dialog.ShowDialog() == true;
            return new SubmitForReviewDialogResult
            {
                Confirmed = result,
                ChangeDescription = dialog.ChangeDescription,
                SelectedReviewer = dialog.SelectedReviewer
            };
        }

        public ReviewDecisionDialogResult ShowReviewDecision(
            string submissionInfo, string gateNote)
        {
            var dialog = new ReviewDecisionDialog();
            if (!string.IsNullOrWhiteSpace(submissionInfo)) dialog.SetSubmissionInfo(submissionInfo);
            if (!string.IsNullOrWhiteSpace(gateNote)) dialog.ShowGateNote(gateNote);

            var result = dialog.ShowDialog() == true;
            return new ReviewDecisionDialogResult
            {
                Confirmed = result,
                Kind = dialog.Decision == ReviewDecision.Approve
                    ? CadBusinessActionKind.Approve
                    : dialog.Decision == ReviewDecision.RequestRework
                        ? CadBusinessActionKind.RequestRework
                        : CadBusinessActionKind.Checkout,
                Comment = dialog.Comment
            };
        }

        public bool ShowWithdrawConfirm(string submissionInfo)
        {
            var dialog = new WithdrawConfirmDialog();
            if (!string.IsNullOrWhiteSpace(submissionInfo)) dialog.SetSubmissionInfo(submissionInfo);
            return dialog.ShowDialog() == true && dialog.Confirmed;
        }

        public bool ShowGatePending(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        public bool ShowReviewerUnavailable(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        public bool ConfirmSimple(string title, string message)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                   == MessageBoxResult.Yes;
        }
    }
}
