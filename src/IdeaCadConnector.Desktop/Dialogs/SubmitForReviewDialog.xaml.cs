using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class SubmitForReviewDialog : Window
    {
        public string ChangeDescription { get; private set; }

        public SubmitForReviewDialog()
        {
            InitializeComponent();
            ReviewerGateNote.Text = "Reviewer assignment is unavailable until authority evidence is completed.";
        }

        public void SetCadInfo(string text) => CadInfo.Text = text;
        public void SetPartInfo(string text) => PartInfo.Text = text;

        public void SetAvailableReviewers(IReadOnlyList<string> reviewers)
        {
            if (reviewers != null && reviewers.Count > 0)
            {
                ReviewerCombo.ItemsSource = reviewers;
                ReviewerCombo.SelectedIndex = 0;
            }
            ReviewerCombo.IsEnabled = false;
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChangeDescriptionBox.Text))
            {
                MessageBox.Show("Change description is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ChangeDescription = ChangeDescriptionBox.Text.Trim();
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
