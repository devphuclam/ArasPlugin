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

        private void OnExpandAllClick(object sender, RoutedEventArgs e)
        {
            SetTreeExpansion(StructureTree, true);
        }

        private void OnCollapseAllClick(object sender, RoutedEventArgs e)
        {
            SetTreeExpansion(StructureTree, false);
        }

        private void DocumentListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            var document = listBox?.SelectedItem as PdmDocumentItem;
            if (document?.CanOpen == true)
            {
                var viewModel = DataContext as PdmProjectsViewModel;
                viewModel?.OpenDocumentCommand?.Execute(document);
            }
        }

        private static void SetTreeExpansion(ItemsControl parent, bool isExpanded)
        {
            if (parent == null)
            {
                return;
            }

            parent.UpdateLayout();
            for (var index = 0; index < parent.Items.Count; index++)
            {
                var container = parent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                if (container == null)
                {
                    continue;
                }

                container.IsExpanded = isExpanded;
                if (container.Items.Count > 0)
                {
                    SetTreeExpansion(container, isExpanded);
                }
            }
        }
    }
}
