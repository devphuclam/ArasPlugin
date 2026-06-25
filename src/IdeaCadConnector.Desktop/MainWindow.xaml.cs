using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IdeaCadConnector.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _isSidebarCollapsed;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox == null || _viewModel == null)
                return;

            _viewModel.LoginViewModel.Password = passwordBox.Password;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateResponsiveLayout(ActualWidth);
        }

        private void OnHomeNavigationClick(object sender, RoutedEventArgs e)
        {
            ShowExistingWorkspace(HomeNavigationButton);
        }

        private void OnSearchNavigationClick(object sender, RoutedEventArgs e)
        {
            ShowExistingWorkspace(SearchNavigationButton);
        }

        private void OnPdmProjectsNavigationClick(object sender, RoutedEventArgs e)
        {
            ExistingWorkspace.Visibility = Visibility.Collapsed;
            PdmWorkspace.Visibility = Visibility.Visible;
            SetActiveNavigation(PdmProjectsNavigationButton);
        }

        private void ShowExistingWorkspace(Button activeButton)
        {
            PdmWorkspace.Visibility = Visibility.Collapsed;
            ExistingWorkspace.Visibility = Visibility.Visible;
            SetActiveNavigation(activeButton);
        }

        private void SetActiveNavigation(Button activeButton)
        {
            var transparent = Brushes.Transparent;
            HomeNavigationButton.Background = transparent;
            HomeNavigationButton.BorderBrush = transparent;
            SearchNavigationButton.Background = transparent;
            SearchNavigationButton.BorderBrush = transparent;
            PdmProjectsNavigationButton.Background = transparent;
            PdmProjectsNavigationButton.BorderBrush = transparent;

            activeButton.Background = FindResource("SidebarActiveBrush") as Brush;
            activeButton.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x90, 0xB8, 0xFF));
        }

        private void OnSidebarCollapseClick(object sender, RoutedEventArgs e)
        {
            _isSidebarCollapsed = !_isSidebarCollapsed;
            if (_isSidebarCollapsed)
            {
                SidebarColumn.Width = new GridLength(92);
                SidebarCollapseIcon.Text = "\uE76C";
            }
            else
            {
                SidebarColumn.Width = new GridLength(232);
                SidebarCollapseIcon.Text = "\uE76B";
            }
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double width)
        {
            var compact = width < 1380;
            var narrow = width < 1180;

            if (compact)
            {
                MainContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                MainContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
                MainContentGrid.ColumnDefinitions[2].Width = new GridLength(0);
                MainContentGrid.RowDefinitions[0].Height = GridLength.Auto;
                MainContentGrid.RowDefinitions[1].Height = GridLength.Auto;

                Grid.SetRow(ResultsCard, 0);
                Grid.SetColumn(ResultsCard, 0);
                Grid.SetColumnSpan(ResultsCard, 3);

                Grid.SetRow(DetailsPanel, 1);
                Grid.SetColumn(DetailsPanel, 0);
                Grid.SetColumnSpan(DetailsPanel, 3);
                DetailsPanel.Margin = new Thickness(0, 16, 0, 0);
            }
            else
            {
                MainContentGrid.ColumnDefinitions[0].Width = new GridLength(2.55, GridUnitType.Star);
                MainContentGrid.ColumnDefinitions[1].Width = new GridLength(18);
                MainContentGrid.ColumnDefinitions[2].Width = new GridLength(1.18, GridUnitType.Star);
                MainContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                MainContentGrid.RowDefinitions[1].Height = new GridLength(0);

                Grid.SetRow(ResultsCard, 0);
                Grid.SetColumn(ResultsCard, 0);
                Grid.SetColumnSpan(ResultsCard, 1);

                Grid.SetRow(DetailsPanel, 0);
                Grid.SetColumn(DetailsPanel, 2);
                Grid.SetColumnSpan(DetailsPanel, 1);
                DetailsPanel.Margin = new Thickness(0);
            }

            if (narrow)
            {
                HeaderGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                HeaderGrid.ColumnDefinitions[1].Width = new GridLength(0);

                if (HeaderGrid.RowDefinitions.Count == 0)
                {
                    HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                Grid.SetColumnSpan(HeaderGrid.Children[0], 1);
                Grid.SetColumn(HeaderGrid.Children[0], 0);
                Grid.SetRow(HeaderGrid.Children[0], 0);
                Grid.SetColumn(HeaderGrid.Children[1], 0);
                Grid.SetColumnSpan(HeaderGrid.Children[1], 1);
                Grid.SetRow(HeaderGrid.Children[1], 1);

                LoginGrid.ColumnDefinitions[0].Width = new GridLength(0);
                LoginGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                LoginGrid.ColumnDefinitions[2].Width = new GridLength(0);
                LoginGrid.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
                LoginGrid.ColumnDefinitions[4].Width = new GridLength(130);

                SearchGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                if (HeaderGrid.RowDefinitions.Count > 0)
                {
                    HeaderGrid.RowDefinitions.Clear();
                }

                HeaderGrid.ColumnDefinitions[1].Width = new GridLength(330);

                Grid.SetColumnSpan(HeaderGrid.Children[0], 1);
                Grid.SetColumn(HeaderGrid.Children[0], 0);
                Grid.SetRow(HeaderGrid.Children[0], 0);
                Grid.SetColumn(HeaderGrid.Children[1], 1);
                Grid.SetColumnSpan(HeaderGrid.Children[1], 1);
                Grid.SetRow(HeaderGrid.Children[1], 0);

                LoginGrid.ColumnDefinitions[0].Width = new GridLength(110);
                LoginGrid.ColumnDefinitions[1].Width = new GridLength(220);
                LoginGrid.ColumnDefinitions[2].Width = new GridLength(130);
                LoginGrid.ColumnDefinitions[3].Width = new GridLength(240);
                LoginGrid.ColumnDefinitions[4].Width = new GridLength(140);
                LoginGrid.ColumnDefinitions[5].Width = new GridLength(1, GridUnitType.Star);

                SearchGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }
}
