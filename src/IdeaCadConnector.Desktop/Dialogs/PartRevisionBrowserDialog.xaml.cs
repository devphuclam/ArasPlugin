using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class PartRevisionBrowserDialog : Window
    {
        public PartRevisionBrowserDialog()
        {
            InitializeComponent();
        }

        internal PartRevisionBrowserDialog(PartRevisionBrowserViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
