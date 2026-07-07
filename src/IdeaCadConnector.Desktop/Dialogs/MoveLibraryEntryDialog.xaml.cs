using System.Windows;

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
    }
}
