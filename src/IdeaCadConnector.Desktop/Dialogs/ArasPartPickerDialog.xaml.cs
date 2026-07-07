using System.Windows;
using System.Windows.Input;

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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
