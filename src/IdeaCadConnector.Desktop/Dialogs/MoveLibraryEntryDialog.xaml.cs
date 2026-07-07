using System.Windows;
using System.Windows.Input;

namespace IdeaCadConnector.Desktop
{
    public partial class MoveLibraryEntryDialog : Window
    {
        public MoveLibraryEntryDialog()
        {
            InitializeComponent();
        }

        internal MoveLibraryEntryDialog(MoveLibraryEntryDialogViewModel viewModel)
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
