using System.Windows;

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
    }
}
