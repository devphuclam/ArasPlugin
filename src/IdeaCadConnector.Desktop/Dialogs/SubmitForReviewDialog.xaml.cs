using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class SubmitForReviewDialog : Window
    {
        public string ChangeDescription { get; private set; }
        public string SelectedReviewer { get; private set; }

        public SubmitForReviewDialog()
        {
            InitializeComponent();
        }

        public void SetCadInfo(string text) => CadInfo.Text = text;
        public void SetPartInfo(string text) => PartInfo.Text = text;
        public void SetReviewers(System.Collections.Generic.IEnumerable<string> reviewers)
        {
            foreach (var r in reviewers)
                ReviewerCombo.Items.Add(r);
            if (ReviewerCombo.Items.Count > 0)
                ReviewerCombo.SelectedIndex = 0;
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChangeDescriptionBox.Text))
            {
                MessageBox.Show("Change description is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ReviewerCombo.SelectedItem == null)
            {
                MessageBox.Show("Please select a reviewer.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ChangeDescription = ChangeDescriptionBox.Text.Trim();
            SelectedReviewer = ReviewerCombo.SelectedItem.ToString();
            DialogResult = true;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
