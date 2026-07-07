using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class SaveToLibraryDialog : Window
    {
        public SaveToLibraryDialog()
        {
            InitializeComponent();
        }

        internal SaveToLibraryDialog(SaveToLibraryDialogViewModel viewModel)
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
