using System.Windows;

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
    }
}
