using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeuroProstheticSuite
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _dataTimer;

        public MainWindow()
        {
            InitializeComponent();

            SetupLiveCharts();
            SetupDataTimer();
            DataContext = this;

            if (this.FindName("GridStaticAnalysis") is UIElement gridStatic)
            {
                gridStatic.Visibility = Visibility.Collapsed;
            }

            if (CmbPorts != null)
            {
                CmbPorts.ItemsSource = new string[] { "COM1 (Simulasi)" };
                if (CmbPorts.Items.Count > 0)
                    CmbPorts.SelectedIndex = 0;
            }

            if (TxtConnectionStatus != null)
            {
                TxtConnectionStatus.Text = "Status: Simulation Mode";
                TxtConnectionStatus.Foreground = Brushes.Orange;
            }

            // Show dashboard by default
            ContentRegion.Content = new DashboardView();
        }

        private void SetupLiveCharts()
        {
            // placeholder for chart initialization if you integrate LiveCharts later
        }

        private void SetupDataTimer()
        {
            _dataTimer = new DispatcherTimer();
            _dataTimer.Interval = System.TimeSpan.FromMilliseconds(500);
            _dataTimer.Tick += (s, e) =>
            {
                // periodic updates if needed
            };
            _dataTimer.Start();
        }

        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            ContentRegion.Content = new DashboardView();
        }

        private void NavigateToStaticAnalysis(object sender, RoutedEventArgs e)
        {
            ContentRegion.Content = new StaticAnalysisView();
        }

        private void NavigateToModeling(object sender, RoutedEventArgs e)
        {
            ContentRegion.Content = new PlaceholderView("Pemodelan Kontrol");
        }

        private void NavigateToStatistics(object sender, RoutedEventArgs e)
        {
            ContentRegion.Content = new PlaceholderView("Statistik (FSR & Suhu)");
        }
    }
}
