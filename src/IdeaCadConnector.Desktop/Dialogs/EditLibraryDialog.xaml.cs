using System.Windows;

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
    }
}
