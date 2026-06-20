using System.Windows;
using System.Windows.Controls;

namespace IdeaCadConnector.Desktop
{
    public partial class PdmProjectsView : UserControl
    {
        public PdmProjectsView()
        {
            InitializeComponent();
            DataContext = new PdmProjectsViewModel();
        }

        private void OnStructureSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as PdmProjectsViewModel;
            var node = e.NewValue as PdmStructureNode;
            if (viewModel != null && node != null)
            {
                viewModel.SelectedNode = node;
            }
        }
    }
}
