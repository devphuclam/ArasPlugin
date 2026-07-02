using System.Windows;

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
    }
}
