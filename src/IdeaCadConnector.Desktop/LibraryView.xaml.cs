using System.Windows.Controls;

namespace IdeaCadConnector.Desktop
{
    public partial class LibraryView : UserControl
    {
        public LibraryView()
        {
            InitializeComponent();
            if (DataContext == null)
                DataContext = new LibraryViewModel();
        }
    }
}
