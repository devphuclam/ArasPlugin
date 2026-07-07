using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class EditLibraryDialog : Window
    {
        public EditLibraryDialog()
        {
            InitializeComponent();
        }

        internal EditLibraryDialog(EditLibraryDialogViewModel viewModel)
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
