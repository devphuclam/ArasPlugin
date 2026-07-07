using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class CreateLibraryDialog : Window
    {
        public CreateLibraryDialog()
        {
            InitializeComponent();
        }

        internal CreateLibraryDialog(CreateLibraryDialogViewModel viewModel)
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
