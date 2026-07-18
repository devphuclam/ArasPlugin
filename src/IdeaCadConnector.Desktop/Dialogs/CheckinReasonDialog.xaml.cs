using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class CheckinReasonDialog : Window
    {
        public string Reason { get; private set; }

        public CheckinReasonDialog()
        {
            InitializeComponent();
        }

        private void ReasonTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = ReasonTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                OkButton.IsEnabled = false;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
            else
            {
                OkButton.IsEnabled = true;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var text = ReasonTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                ValidationMessage.Text = "Reason cannot be empty.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            Reason = text.Trim();
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
