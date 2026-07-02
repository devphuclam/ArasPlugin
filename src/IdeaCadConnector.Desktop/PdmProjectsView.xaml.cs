using System.Windows;
using System.Windows.Controls;
using IdeaCadConnector.Core.Localization;
using IdeaCadConnector.Desktop.Services;

namespace IdeaCadConnector.Desktop
{
    public partial class PdmProjectsView : UserControl
    {
        private bool _isLeftPanelCollapsed;
        private bool _isPdmStructureCollapsed;
        private bool _isCadStructureCollapsed;
        private readonly GridLength _originalLeftPanelWidth;
        private readonly GridLength _originalMiddlePanelWidth;
        private static readonly GridLength CollapsedPanelWidth = new GridLength(56);

        public PdmProjectsView()
        {
            InitializeComponent();
            var viewModel = new PdmProjectsViewModel();
            DataContext = viewModel;
            AppSessionContext.Current.CurrentPdmProjectsViewModel = viewModel;
            _originalLeftPanelWidth = LeftPanelColumn.Width;
            _originalMiddlePanelWidth = MiddlePanelColumn.Width;
            LocalizationSource.Instance.PropertyChanged += OnLocalizationChanged;
        }

        private void OnLocalizationChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item[]" && DataContext is PdmProjectsViewModel vm)
            {
                vm.RefreshLocalization();
            }
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

        private void OnPdmStructureToggleClick(object sender, RoutedEventArgs e)
        {
            _isPdmStructureCollapsed = !_isPdmStructureCollapsed;
            ApplyPdmPanelState();
        }

        private void OnCadStructureToggleClick(object sender, RoutedEventArgs e)
        {
            _isCadStructureCollapsed = !_isCadStructureCollapsed;
            ApplyCadPanelState();
        }

        private void OnLeftPanelToggleClick(object sender, RoutedEventArgs e)
        {
            _isLeftPanelCollapsed = !_isLeftPanelCollapsed;
            if (_isLeftPanelCollapsed)
            {
                LeftPanelColumn.Width = new GridLength(0);
                LeftSpacer1Column.Width = new GridLength(0);
                MiddlePanelColumn.Width = new GridLength(0);
                LeftSpacer2Column.Width = new GridLength(0);
                LeftPanelToggleButton.Content = "\u25B6";
                LeftPanelToggleButton.ToolTip = LocalizationSource.Instance[TranslationKeys.TooltipShowStructurePanels];
            }
            else
            {
                ApplyPdmPanelState();
                ApplyCadPanelState();
                LeftPanelToggleButton.Content = "\u25C0";
                LeftPanelToggleButton.ToolTip = LocalizationSource.Instance[TranslationKeys.TooltipHideStructurePanels];
            }
        }

        private void ApplyPdmPanelState()
        {
            LeftPanelColumn.Width = _isPdmStructureCollapsed ? CollapsedPanelWidth : _originalLeftPanelWidth;
            PdmStructureSlideContent.Visibility = _isPdmStructureCollapsed ? Visibility.Collapsed : Visibility.Visible;
            LeftSpacer1Column.Width = new GridLength(14);
            ApplyPanelHeaderState(PdmStructureToggleBtn, PdmStructureTitle, PdmStructureIcon, _isPdmStructureCollapsed);
        }

        private void ApplyCadPanelState()
        {
            MiddlePanelColumn.Width = _isCadStructureCollapsed ? CollapsedPanelWidth : _originalMiddlePanelWidth;
            CadStructureBody.Visibility = _isCadStructureCollapsed ? Visibility.Collapsed : Visibility.Visible;
            LeftSpacer2Column.Width = new GridLength(14);
            ApplyPanelHeaderState(CadStructureToggleBtn, CadStructureTitle, CadStructureIcon, _isCadStructureCollapsed);
        }

        private static void ApplyPanelHeaderState(Button button, TextBlock title, TextBlock icon, bool isCollapsed)
        {
            if (isCollapsed)
            {
                title.Visibility = Visibility.Collapsed;
                icon.Text = "\u25B6";
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                button.Margin = new Thickness(0, 14, 0, 10);
                button.ToolTip = "Expand panel";
                return;
            }

            title.Visibility = Visibility.Visible;
            icon.Text = "\u25C0";
            icon.HorizontalAlignment = HorizontalAlignment.Right;
            button.Margin = new Thickness(18, 14, 18, 10);
            button.ToolTip = "Collapse panel";
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
