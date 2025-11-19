using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
// using System.IO.Ports; // Dihapus!
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeuroProstheticSuite
{
    public partial class MainWindow : Window
    {
        // 1. Deklarasi Variabel Inti
        private DispatcherTimer dataTimer;
        private Random random = new Random();
        private int sampleCount = 0;
        private const int MaxDataPoints = 200;

        // Data Simulasi/Nyata
        private double simulatedEMG = 0;
        private double simulatedRMS = 0;
        private string[] graspPatterns = { "POWER GRASP", "PINCH GRASP", "TRIPOD GRASP", "RELEASE" };

        // --- LIVECHARTS PROPERTIES ---
        public SeriesCollection RawSignalSeriesCollection { get; set; }
        public SeriesCollection RmsSeriesCollection { get; set; }
        public Func<double, string> YFormatter { get; set; }
        private ObservableCollection<double> rawSignalPoints = new ObservableCollection<double>();
        private ObservableCollection<double> rmsPoints = new ObservableCollection<double>();

        // --- VARIABEL SERIAL PORT Dihapus! ---

        public MainWindow()
        {
            InitializeComponent();
            SetupLiveCharts();
            // SetupSerialPort() Dihapus!
            SetupDataTimer();
            DataContext = this;
            GridStaticAnalysis.Visibility = Visibility.Collapsed;

            if (CmbPorts != null)
            {
                CmbPorts.ItemsSource = new string[] { "COM1 (Simulasi)" };
                CmbPorts.SelectedIndex = 0;
            }

            if (TxtConnectionStatus != null)
            {
                TxtConnectionStatus.Text = "Status: Simulation Mode";
                TxtConnectionStatus.Foreground = Brushes.Orange;
            }
        }

        // --- MANAJEMEN APLIKASI (Window Closing Dihapus karena tidak ada Serial Port) ---

        // --- SETUP DAN TIMER ---

        private void SetupLiveCharts()
        {
            RawSignalSeriesCollection = new SeriesCollection
            {
                new LineSeries {
                    Title = "EMG Filtered (Simulasi)", Values = new ChartValues<double>(rawSignalPoints),
                    LineSmoothness = 0, PointGeometry = null, Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromRgb(135, 206, 235))
                }
            };
            RmsSeriesCollection = new SeriesCollection
            {
                new LineSeries {
                    Title = "RMS Feature (Control Input)", Values = new ChartValues<double>(rmsPoints),
                    LineSmoothness = 0.5, PointGeometrySize = 0, Stroke = new SolidColorBrush(Color.FromRgb(60, 179, 113))
                }
            };
            YFormatter = value => value.ToString("F2");
        }

        private void SetupDataTimer()
        {
            dataTimer = new DispatcherTimer();
            dataTimer.Interval = TimeSpan.FromMilliseconds(50);
            dataTimer.Tick += DataTimer_Tick;
            dataTimer.Start();
        }

        private void DataTimer_Tick(object sender, EventArgs e)
        {
            // Mode Simulasi Penuh: Timer SELALU berjalan
            sampleCount++;
            SimulateSensorData();
            SimulateSignalProcessing();
            SimulateControlOutput();
            UpdateUI();
            UpdateChartData();
        }

        // --- SERIAL PORT MANAGEMENT Dihapus! ---

        // Handler Klik Connect Dihapus!

        // SerialPort_DataReceived Dihapus!

        // --- SIMULATION AND PROCESSING LOGIC ---

        private void SimulateSensorData()
        {
            // Sinyal mentah + Noise AC 50 Hz (untuk simulasi)
            double noise50Hz = 0.3 * Math.Sin(2 * Math.PI * 50 * (sampleCount / 1000.0));
            simulatedEMG = Math.Sin(sampleCount * 0.1) * 0.5 + random.NextDouble() * 0.2 - 0.1 + noise50Hz;
        }

        private void SimulateSignalProcessing()
        {
            double filteredEMG = simulatedEMG * 0.7; // Filter simulasi

            simulatedRMS = simulatedRMS * 0.9 + Math.Abs(filteredEMG) * 0.1;

            rawSignalPoints.Add(filteredEMG * 10);
        }

        private void SimulateControlOutput()
        {
            if (sampleCount % 500 == 0)
            {
                int index = random.Next(graspPatterns.Length);
                txtGripClassification.Text = "Grip Classification: " + graspPatterns[index];
            }
            // Ini adalah area yang akan Anda ganti dengan Kontroler PID digital
            double newKp = 1.0 + simulatedRMS * 5.0;
            txtKpValue.Text = $"Adaptive Kp Parameter: {newKp:F2}";
        }

        private void UpdateUI()
        {
            int fsrValue = (int)Math.Min(100, simulatedRMS * 150);
            txtPressureLevel.Text = $"{fsrValue}%";
            double currentTemp = 28.0 + (random.NextDouble() * 1.5 - 0.75);
            txtTemperature.Text = $"{currentTemp:F1} °C";

            double currentKp;
            if (double.TryParse(txtKpValue.Text.Split(':')[1].Trim(), out currentKp))
            {
                double systemError = Math.Max(0.01, simulatedRMS * 0.15 - (currentKp / 100));
                txtSystemError.Text = $"System Error (e[n]): {systemError:F3} ({(systemError < 0.05 ? "Stable" : "Unstable")})";
            }
        }

        private void UpdateChartData()
        {
            rmsPoints.Add(simulatedRMS * 10);

            if (rawSignalPoints.Count > MaxDataPoints)
            {
                rawSignalPoints.RemoveAt(0);
                rmsPoints.RemoveAt(0);
            }

            ChartRawSignal.Update(true);
            ChartRMS.Update(true);
        }

        // --- LOGIKA NAVIGASI ---
        private void NavigateToDashboard(object sender, RoutedEventArgs e)
        {
            GridRealTimeDashboard.Visibility = Visibility.Visible;
            GridStaticAnalysis.Visibility = Visibility.Collapsed;
            dataTimer.Start(); // Pastikan timer mulai saat kembali ke dashboard
        }

        private void NavigateToStaticAnalysis(object sender, RoutedEventArgs e)
        {
            GridRealTimeDashboard.Visibility = Visibility.Collapsed;
            GridStaticAnalysis.Visibility = Visibility.Visible;
            dataTimer.Stop();
            LoadStaticFFTData();
        }

        private void NavigateToPlaceholder(object sender, RoutedEventArgs e)
        {
            NavigateToDashboard(null, null);

            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                MessageBox.Show($"Fitur '{clickedButton.Content}' akan diimplementasikan pada tahap selanjutnya.",
                                "Fitur Belum Tersedia", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadStaticFFTData()
        {
            ChartValues<double> fftData = new ChartValues<double> { 0.1, 0.2, 0.5, 0.3, 0.1, 0.05, 0.02, 0.01, 0.01 };

            ChartStaticFFT.Series = new SeriesCollection
            {
                new ColumnSeries {
                    Title = "Power Density", Values = fftData,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 215, 0))
                }
            };

            ChartStaticFFT.AxisX.Clear();
            ChartStaticFFT.AxisX.Add(new Axis
            {
                Labels = new string[] { "DC", "4Hz", "8Hz", "15Hz (Alpha)", "20Hz", "50Hz (Notch)", "100Hz", "250Hz", "500Hz" },
                Title = "Frequency (Hz)",
                Foreground = System.Windows.Media.Brushes.White
            });
        }
    }
}