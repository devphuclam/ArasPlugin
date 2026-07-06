using System.Windows;

namespace IdeaCadConnector.Desktop
{
    public partial class ArasPartPickerDialog : Window
    {
        public ArasPartPickerDialog()
        {
            InitializeComponent();
        }

        internal ArasPartPickerDialog(ArasPartPickerViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
