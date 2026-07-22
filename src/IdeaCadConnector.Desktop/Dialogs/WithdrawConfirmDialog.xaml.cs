using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class WithdrawConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }

        public WithdrawConfirmDialog()
        {
            InitializeComponent();
        }

        public void SetSubmissionInfo(string info) => SubmissionInfo.Text = info;

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
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
