using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NeuroProstheticSuite
{
    public partial class DashboardView : UserControl
    {
        private readonly DispatcherTimer _timer;
        private readonly int _emgBufferSize = 512;
        private readonly int _fsrBufferSize = 256;
        private readonly double[] _emgBuffer;
        private readonly double[] _fsrBuffer;
        private int _emgPos = 0;
        private int _fsrPos = 0;
        private bool _running = true;
        private Random _rnd = new Random();

        public DashboardView()
        {
            InitializeComponent();

            _emgBuffer = new double[_emgBufferSize];
            _fsrBuffer = new double[_fsrBufferSize];

            // prefill with small baseline
            for (int i = 0; i < _emgBufferSize; i++) _emgBuffer[i] = 0.0;
            for (int i = 0; i < _fsrBufferSize; i++) _fsrBuffer[i] = 0.0;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50); // default
            _timer.Tick += Timer_Tick;

            Loaded += DashboardView_Loaded;
            SizeChanged += DashboardView_SizeChanged;
        }

        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            _timer.Start();
            _running = true;
            BtnStartStop.Content = "Stop";
            // initial draw
            DrawEmg();
            DrawFsr();
        }

        private void DashboardView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // redraw when sizes change
            DrawEmg();
            DrawFsr();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // generate dummy samples and append to buffers
            double newEmg = GenerateDummyEmgSample();
            double newFsr = GenerateDummyFsrSample();

            AddEmgSample(newEmg);
            AddFsrSample(newFsr);

            // redraw
            DrawEmg();
            DrawFsr();
        }

        // Public API: allow external injection of real data later
        public void AddEmgSample(double value)
        {
            _emgBuffer[_emgPos] = value;
            _emgPos = (_emgPos + 1) % _emgBufferSize;
        }

        public void AddFsrSample(double value)
        {
            _fsrBuffer[_fsrPos] = value;
            _fsrPos = (_fsrPos + 1) % _fsrBufferSize;
        }

        // Dummy generators (replace these with real parsing logic)
        private double _emgPhase = 0;
        private double GenerateDummyEmgSample()
        {
            // bursts + noise
            _emgPhase += 0.12;
            double burst = Math.Exp(-Math.Pow((_emgPhase % (2 * Math.PI)) - Math.PI, 2) / 0.8);
            double val = Math.Sin(_emgPhase * 6) * 0.6 * burst + (_rnd.NextDouble() - 0.5) * 0.08;
            return val;
        }

        private double _fsrPhase = 0;
        private double GenerateDummyFsrSample()
        {
            // slowly varying pressure signal with steps
            _fsrPhase += 0.05;
            double step = 0.5 + 0.4 * (0.5 + 0.5 * Math.Sin(_fsrPhase * 0.25));
            double noise = (_rnd.NextDouble() - 0.5) * 0.02;
            return step + noise;
        }

        // Drawing helpers
        private void DrawEmg()
        {
            if (CanvasEmg == null) return;
            CanvasEmg.Children.Clear();

            double w = CanvasEmg.ActualWidth;
            double h = CanvasEmg.ActualHeight;
            if (w <= 0) w = CanvasEmg.Width; if (h <= 0) h = CanvasEmg.Height;
            if (w <= 0 || h <= 0) return;

            // Get buffer values in chronological order
            var values = new double[_emgBufferSize];
            for (int i = 0; i < _emgBufferSize; i++)
            {
                int idx = (_emgPos + i) % _emgBufferSize;
                values[i] = _emgBuffer[idx];
            }

            double min = values.Min(); double max = values.Max();
            // small guard
            if (Math.Abs(max - min) < 1e-6) { max = min + 1; min = min - 1; }

            // grid midline
            var midLine = new Line
            {
                X1 = 0,
                X2 = w,
                Y1 = h / 2,
                Y2 = h / 2,
                Stroke = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
                StrokeThickness = 1
            };
            CanvasEmg.Children.Add(midLine);

            var poly = new Polyline
            {
                Stroke = Brushes.Lime,
                StrokeThickness = 1.4,
                SnapsToDevicePixels = true
            };

            for (int i = 0; i < values.Length; i++)
            {
                double x = (i / (double)(values.Length - 1)) * w;
                double norm = (values[i] - min) / (max - min);
                double y = (1.0 - norm) * h;
                poly.Points.Add(new Point(x, y));
            }
            CanvasEmg.Children.Add(poly);
        }

        private void DrawFsr()
        {
            if (CanvasFsr == null) return;
            CanvasFsr.Children.Clear();

            double w = CanvasFsr.ActualWidth;
            double h = CanvasFsr.ActualHeight;
            if (w <= 0) w = CanvasFsr.Width; if (h <= 0) h = CanvasFsr.Height;
            if (w <= 0 || h <= 0) return;

            var values = new double[_fsrBufferSize];
            for (int i = 0; i < _fsrBufferSize; i++)
            {
                int idx = (_fsrPos + i) % _fsrBufferSize;
                values[i] = _fsrBuffer[idx];
            }

            double min = values.Min(); double max = values.Max();
            if (Math.Abs(max - min) < 1e-6) { max = min + 1; min = min - 1; }

            // baseline
            var baseLine = new Line
            {
                X1 = 0,
                X2 = w,
                Y1 = h - 2,
                Y2 = h - 2,
                Stroke = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
                StrokeThickness = 1
            };
            CanvasFsr.Children.Add(baseLine);

            var poly = new Polyline
            {
                Stroke = Brushes.Orange,
                StrokeThickness = 2.0,
                SnapsToDevicePixels = true
            };

            for (int i = 0; i < values.Length; i++)
            {
                double x = (i / (double)(values.Length - 1)) * w;
                double norm = (values[i] - min) / (max - min);
                double y = (1.0 - norm) * h;
                poly.Points.Add(new Point(x, y));
            }
            CanvasFsr.Children.Add(poly);
        }

        // UI: start/stop button
        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_running)
            {
                _timer.Stop();
                BtnStartStop.Content = "Start";
                _running = false;
            }
            else
            {
                // try parse interval from textbox
                if (int.TryParse(TxtInterval.Text, out int ms) && ms > 0)
                {
                    _timer.Interval = TimeSpan.FromMilliseconds(ms);
                }
                _timer.Start();
                BtnStartStop.Content = "Stop";
                _running = true;
            }
        }

        private void BtnSetInterval_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtInterval.Text, out int ms) && ms > 0)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(ms);
            }
        }

        // Call when disposing if needed
        public void Stop()
        {
            _timer?.Stop();
        }
    }
}