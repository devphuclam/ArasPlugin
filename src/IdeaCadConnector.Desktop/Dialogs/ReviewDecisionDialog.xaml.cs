using System.Windows;

namespace IdeaCadConnector.Desktop
{
    public enum ReviewDecision
    {
        None,
        Approve,
        RequestRework
    }

    public partial class ReviewDecisionDialog : Window
    {
        public ReviewDecision Decision { get; private set; } = ReviewDecision.None;
        public string Comment { get; private set; }

        public ReviewDecisionDialog()
        {
            InitializeComponent();
        }

        public void SetSubmissionInfo(string info) => SubmissionInfo.Text = info;

        public void ShowGateNote(string message)
        {
            GateNote.Visibility = Visibility.Visible;
            GateNoteText.Text = message;
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            Decision = ReviewDecision.Approve;
            Comment = CommentBox.Text?.Trim();
            DialogResult = true;
            Close();
        }

        private void ReworkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommentBox.Text))
            {
                MessageBox.Show("A comment is required when requesting rework.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Decision = ReviewDecision.RequestRework;
            Comment = CommentBox.Text.Trim();
            DialogResult = true;
            Close();
        }

    }
}
