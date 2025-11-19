using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace NeuroProstheticSuite
{
    public partial class StaticAnalysisView : UserControl
    {
        // Camera spherical coords
        private double _camAzimuth = Math.PI * 0.85; // radians
        private double _camElevation = Math.PI * 0.15;
        private double _camDistance = 4.8;
        private Point3D _camTarget = new Point3D(0.4, 0.0, 0.0);

        // Mouse interaction
        private Point _lastMousePos;
        private bool _isLeftDown = false;
        private bool _isMiddleDown = false;

        // Sensor storage for 3D->2D labels
        private readonly List<SensorMarker> _sensors = new List<SensorMarker>();

        public StaticAnalysisView()
        {
            InitializeComponent();
            Loaded += StaticAnalysisView_Loaded;
            SizeChanged += StaticAnalysisView_SizeChanged;
        }

        private void StaticAnalysisView_Loaded(object sender, RoutedEventArgs e)
        {
            RenderSampleSignals();
            Build3DConcept();
            UpdateCamera();
            UpdateLabels(); // position labels after initial camera setup
            // initialize slider with current cam distance
            SliderZoom.Value = _camDistance;
        }

        private void StaticAnalysisView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderSampleSignals();
            UpdateLabels();
        }

        // --- Static plotting (unchanged) ---
        private void RenderSampleSignals()
        {
            double[] emg = SampleEmgStatic();
            double[] fsr = SampleFsrStatic();
            DrawPolylineOnCanvas(CanvasEmg, emg, Colors.Lime, strokeThickness: 1.6, padding: 6);
            DrawPolylineOnCanvas(CanvasFsr, fsr, Colors.Orange, strokeThickness: 2.0, padding: 6);
        }

        private double[] SampleEmgStatic()
        {
            int n = 400;
            double[] arr = new double[n];
            var rnd = new Random(123);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)n;
                double v = (rnd.NextDouble() - 0.5) * 0.04;
                if (i > n * 0.38 && i < n * 0.62)
                    v += Math.Sin(18 * Math.PI * t) * 0.8 * Math.Exp(-12 * Math.Abs(t - 0.5));
                arr[i] = v;
            }
            return arr;
        }

        private double[] SampleFsrStatic()
        {
            int n = 120;
            double[] arr = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i < n * 0.25) arr[i] = 0.05 + 0.005 * Math.Sin(i * 0.3);
                else if (i < n * 0.7) arr[i] = 0.72 + 0.015 * Math.Sin(i * 0.2);
                else arr[i] = 0.45 + 0.01 * Math.Sin(i * 0.25);
            }
            return arr;
        }

        private void DrawPolylineOnCanvas(Canvas canvas, double[] values, Color color, double strokeThickness = 1.5, double padding = 4)
        {
            if (canvas == null || values == null || values.Length == 0) return;

            canvas.Children.Clear();

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;

            if (w <= 0) w = canvas.Width <= 0 ? 600 : canvas.Width;
            if (h <= 0) h = canvas.Height <= 0 ? 100 : canvas.Height;

            double left = padding, top = padding, right = padding, bottom = padding;
            double drawW = Math.Max(10, w - left - right);
            double drawH = Math.Max(10, h - top - bottom);

            double min = values.Min();
            double max = values.Max();
            if (Math.Abs(max - min) < 1e-9)
            {
                max = min + 1.0;
                min = min - 1.0;
            }

            var mid = new Line
            {
                X1 = left,
                X2 = left + drawW,
                Y1 = top + drawH / 2,
                Y2 = top + drawH / 2,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 1
            };
            canvas.Children.Add(mid);

            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = strokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                SnapsToDevicePixels = true
            };

            for (int i = 0; i < values.Length; i++)
            {
                double x = left + (i / (double)(values.Length - 1)) * drawW;
                double norm = (values[i] - min) / (max - min);
                double y = top + (1.0 - norm) * drawH;
                poly.Points.Add(new Point(x, y));
            }

            canvas.Children.Add(poly);
        }

        // --- 3D building (improved visuals) with sensor registration for labels ---
        private void Build3DConcept()
        {
            Viewport.Children.Clear();
            Viewport.Camera = MainCamera;

            // Lights
            var ambient = new AmbientLight(Color.FromRgb(150, 150, 150));
            var dir = new DirectionalLight(Colors.White, new Vector3D(-1, -1, -2));
            var lightGroup = new Model3DGroup();
            lightGroup.Children.Add(ambient);
            lightGroup.Children.Add(dir);
            Viewport.Children.Add(new ModelVisual3D { Content = lightGroup });

            // Grid floor
            var gridGroup = new Model3DGroup();
            for (double x = -1.0; x <= 2.0; x += 0.25)
            {
                gridGroup.Children.Add(CreateTube(new Point3D(x, -0.25, -1.2), new Point3D(x, -0.25, 1.2), 0.002, Colors.Gray));
            }
            for (double z = -1.2; z <= 1.2; z += 0.25)
            {
                gridGroup.Children.Add(CreateTube(new Point3D(-1.0, -0.25, z), new Point3D(2.0, -0.25, z), 0.002, Colors.Gray));
            }
            Viewport.Children.Add(new ModelVisual3D { Content = gridGroup });

            // Axes
            var axes = CreateAxes(length: 2.2, thickness: 0.01);
            Viewport.Children.Add(new ModelVisual3D { Content = axes });

            // Forearm/casing (box)
            var arm = CreateBoxModel(new Point3D(0.4, 0.1, 0), 1.4, 0.28, 0.28, Colors.DimGray);
            Viewport.Children.Add(new ModelVisual3D { Content = arm });

            // Wrist/casing
            var wrist = CreateBoxModel(new Point3D(1.05, -0.02, 0), 0.36, 0.18, 0.18, Color.FromRgb(48, 48, 48));
            Viewport.Children.Add(new ModelVisual3D { Content = wrist });

            // Fingers segments
            var fingerOffsets = new[] { 0.06, -0.02, -0.1 };
            for (int i = 0; i < fingerOffsets.Length; i++)
            {
                var fx = 1.25;
                var fy = -0.02 + fingerOffsets[i];
                var fz = 0.06 - i * 0.06;
                var finger1 = CreateBoxModel(new Point3D(fx, fy, fz), 0.28, 0.06, 0.04, Color.FromRgb(70, 70, 70));
                Viewport.Children.Add(new ModelVisual3D { Content = finger1 });
                var fingerTip = CreateBoxModel(new Point3D(fx + 0.16, fy, fz), 0.12, 0.04, 0.035, Color.FromRgb(90, 90, 90));
                Viewport.Children.Add(new ModelVisual3D { Content = fingerTip });
            }

            // Register sensors and add their 3D visuals
            _sensors.Clear();
            // FSR sensors (on finger tips)
            var fsrPositions = new[]
            {
                new Point3D(1.36, 0.03, 0.06), // finger 1 (top)
                new Point3D(1.36, -0.01, 0.0), // finger 2 (middle)
                new Point3D(1.36, -0.08, -0.06) // finger 3 (bottom)
            };
            // choose label offsets to avoid overlap (in pixels)
            var fsrLabelOffsets = new[] { new Vector(40, -30), new Vector(40, -6), new Vector(40, 18) };
            for (int i = 0; i < fsrPositions.Length; i++)
            {
                var p = fsrPositions[i];
                var s = CreateSphereModel(p, 0.055, Colors.Gold, tDiv: 20, pDiv: 16);
                Viewport.Children.Add(new ModelVisual3D { Content = s });
                var conn = CreateTube(new Point3D(1.05, -0.02, 0), p, 0.01, Colors.Gold);
                Viewport.Children.Add(new ModelVisual3D { Content = conn });

                RegisterSensor($"FSR {i + 1}", p, Colors.Gold, fsrLabelOffsets[i]);
            }

            // TTP223 touch
            var ttpPos = new Point3D(1.0, 0.08, 0.0);
            Viewport.Children.Add(new ModelVisual3D { Content = CreateSphereModel(ttpPos, 0.045, Colors.DeepSkyBlue, tDiv: 18, pDiv: 14) });
            Viewport.Children.Add(new ModelVisual3D { Content = CreateTube(new Point3D(1.05, -0.02, 0), ttpPos, 0.008, Colors.DeepSkyBlue) });
            RegisterSensor("TTP223", ttpPos, Colors.DeepSkyBlue, new Vector(40, -30));

            // EMG (MyoWare placement)
            var emgPos = new Point3D(-0.15, 0.12, 0.10);
            Viewport.Children.Add(new ModelVisual3D { Content = CreateSphereModel(emgPos, 0.07, Colors.HotPink, tDiv: 20, pDiv: 16) });
            Viewport.Children.Add(new ModelVisual3D { Content = CreateTube(new Point3D(0.0, 0.06, 0.0), emgPos, 0.008, Colors.HotPink) });
            RegisterSensor("EMG", emgPos, Colors.HotPink, new Vector(-60, -30));

            // After building 3D, create/update 2D labels + connectors
            CreateOrUpdateLabels();
            UpdateLabels();
        }

        // Register sensor into _sensors list with preferred label offset (in pixels)
        private void RegisterSensor(string name, Point3D pos, Color color, Vector labelOffset)
        {
            _sensors.Add(new SensorMarker { Name = name, Position = pos, Color = color, LabelOffset = labelOffset });
        }

        // Create TextBlock labels on overlay canvas (or update existing)
        private void CreateOrUpdateLabels()
        {
            OverlayCanvas.Children.Clear();
            foreach (var s in _sensors)
            {
                // Compose a horizontal StackPanel for marker+text inside a Border
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(s.Color), Margin = new Thickness(0, 0, 6, 0) });
                sp.Children.Add(new TextBlock
                {
                    Text = s.Name,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                });

                var border = new Border
                {
                    Child = sp,
                    Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 24)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 3, 6, 3),
                    Opacity = 0.98
                };

                // create connector shapes (line + arrowhead)
                var connectorLine = new Line
                {
                    Stroke = new SolidColorBrush(s.Color),
                    StrokeThickness = 1.8,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    Visibility = Visibility.Collapsed
                };
                var arrow = new Polygon
                {
                    Fill = new SolidColorBrush(s.Color),
                    Visibility = Visibility.Collapsed
                };

                // add elements: connector behind labels - so add line and arrow first
                OverlayCanvas.Children.Add(connectorLine);
                OverlayCanvas.Children.Add(arrow);
                OverlayCanvas.Children.Add(border);

                s.LabelElement = border;
                s.ConnectorLine = connectorLine;
                s.ConnectorArrow = arrow;
            }
        }

        // Update label positions (project 3D -> 2D) and connector geometry
        private void UpdateLabels()
        {
            // Ensure overlay covers the viewport size
            OverlayCanvas.Width = Viewport.ActualWidth;
            OverlayCanvas.Height = Viewport.ActualHeight;

            double vw = Viewport.ActualWidth;
            double vh = Viewport.ActualHeight;
            if (vw <= 0 || vh <= 0) return;

            foreach (var s in _sensors)
            {
                var screen = ProjectToViewport(s.Position, MainCamera, new Size(vw, vh));
                if (screen.HasValue && s.LabelElement != null)
                {
                    var pt = screen.Value;
                    s.LabelElement.Visibility = Visibility.Visible;

                    // Measure label
                    s.LabelElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var desired = s.LabelElement.DesiredSize;

                    // Compute label position using preferred offset (avoid overlap)
                    double left = pt.X + s.LabelOffset.X - desired.Width / 2.0;
                    double top = pt.Y + s.LabelOffset.Y - desired.Height / 2.0;

                    // Keep label within overlay bounds
                    left = Math.Max(4, Math.Min(OverlayCanvas.Width - desired.Width - 4, left));
                    top = Math.Max(4, Math.Min(OverlayCanvas.Height - desired.Height - 4, top));

                    Canvas.SetLeft(s.LabelElement, left);
                    Canvas.SetTop(s.LabelElement, top);

                    // Connector: from label center edge toward marker projection
                    var labelCenter = new Point(left + desired.Width / 2.0, top + desired.Height / 2.0);

                    // We'll draw a line from a point on label border toward the 3D projected point.
                    // Compute direction from label center to marker point
                    Vector dir = pt - labelCenter;
                    if (dir.Length < 1e-6) dir = new Vector(0, -1);
                    dir.Normalize();

                    // Start of connector: a point on label border (offset from center toward marker)
                    double labelRadius = Math.Max(desired.Width, desired.Height) / 2.0;
                    // use half width/height to compute intersection roughly
                    var start = new Point(
                        labelCenter.X + dir.X * (Math.Max(desired.Width, desired.Height) * 0.5 + 2),
                        labelCenter.Y + dir.Y * (Math.Max(desired.Width, desired.Height) * 0.5 + 2)
                    );

                    // End point at marker projection (slightly offset toward label to avoid covering sphere)
                    var end = new Point(pt.X, pt.Y);

                    // Update line
                    if (s.ConnectorLine != null)
                    {
                        s.ConnectorLine.X1 = start.X;
                        s.ConnectorLine.Y1 = start.Y;
                        s.ConnectorLine.X2 = end.X;
                        s.ConnectorLine.Y2 = end.Y;
                        s.ConnectorLine.Visibility = Visibility.Visible;
                    }

                    // Arrowhead: small triangle pointing to the marker
                    if (s.ConnectorArrow != null)
                    {
                        // base direction from start->end
                        Vector v = start - end;
                        if (v.Length < 1e-6) v = new Vector(0, -1);
                        v.Normalize();
                        // perpendicular
                        Vector p = new Vector(-v.Y, v.X);

                        double arrowLen = 10; // pixels
                        Point p1 = end; // tip
                        Point p2 = end + v * arrowLen + p * (arrowLen * 0.45);
                        Point p3 = end + v * arrowLen - p * (arrowLen * 0.45);

                        s.ConnectorArrow.Points.Clear();
                        s.ConnectorArrow.Points.Add(p1);
                        s.ConnectorArrow.Points.Add(p2);
                        s.ConnectorArrow.Points.Add(p3);
                        s.ConnectorArrow.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (s.LabelElement != null) s.LabelElement.Visibility = Visibility.Collapsed;
                    if (s.ConnectorLine != null) s.ConnectorLine.Visibility = Visibility.Collapsed;
                    if (s.ConnectorArrow != null) s.ConnectorArrow.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Project a 3D world point into viewport 2D pixel coordinates
        // Handles PerspectiveCamera and OrthographicCamera properly.
        private Point? ProjectToViewport(Point3D point, ProjectionCamera camera, Size viewportSize)
        {
            // Camera basis
            Vector3D forward = camera.LookDirection;
            forward.Normalize();
            Vector3D up = camera.UpDirection;
            up.Normalize();
            Vector3D right = Vector3D.CrossProduct(forward, up);
            right.Normalize();
            // recompute true up to ensure orthogonality
            up = Vector3D.CrossProduct(right, forward);
            up.Normalize();

            // vector from camera to point
            var rel = new Vector3D(point.X - camera.Position.X, point.Y - camera.Position.Y, point.Z - camera.Position.Z);

            // coordinates in camera space
            double cx = Vector3D.DotProduct(rel, right);
            double cy = Vector3D.DotProduct(rel, up);
            double cz = Vector3D.DotProduct(rel, forward);

            // if cz <= 0 then point is behind camera
            if (cz <= 1e-6) return null;

            double aspect = viewportSize.Width / Math.Max(1.0, viewportSize.Height);

            if (camera is PerspectiveCamera pc)
            {
                // Perspective projection
                double fovRad = pc.FieldOfView * Math.PI / 180.0;
                double tan = Math.Tan(fovRad / 2.0);

                double x_ndc = (cx / cz) / (tan * aspect);
                double y_ndc = (cy / cz) / tan;

                if (double.IsNaN(x_ndc) || double.IsNaN(y_ndc)) return null;

                double screenX = (x_ndc + 1.0) * 0.5 * viewportSize.Width;
                double screenY = (1.0 - y_ndc) * 0.5 * viewportSize.Height;
                return new Point(screenX, screenY);
            }
            else if (camera is OrthographicCamera oc)
            {
                // Orthographic projection: map world units to NDC using camera.Width
                double widthWorld = oc.Width;
                if (widthWorld <= 1e-9) widthWorld = 1.0;
                double heightWorld = widthWorld / aspect;

                double x_ndc = cx / (widthWorld / 2.0);
                double y_ndc = cy / (heightWorld / 2.0);

                if (double.IsNaN(x_ndc) || double.IsNaN(y_ndc)) return null;

                double screenX = (x_ndc + 1.0) * 0.5 * viewportSize.Width;
                double screenY = (1.0 - y_ndc) * 0.5 * viewportSize.Height;
                return new Point(screenX, screenY);
            }
            else
            {
                // Fallback: treat as a perspective camera with reasonable FOV
                double fovRad = Math.PI / 3.0;
                double tan = Math.Tan(fovRad / 2.0);

                double x_ndc = (cx / cz) / (tan * aspect);
                double y_ndc = (cy / cz) / tan;

                if (double.IsNaN(x_ndc) || double.IsNaN(y_ndc)) return null;

                double screenX = (x_ndc + 1.0) * 0.5 * viewportSize.Width;
                double screenY = (1.0 - y_ndc) * 0.5 * viewportSize.Height;
                return new Point(screenX, screenY);
            }
        }

        // Update camera position from spherical coords and then update label positions
        private void UpdateCamera()
        {
            // clamp elevation to avoid flip
            _camElevation = Math.Max(0.05, Math.Min(Math.PI - 0.05, _camElevation));
            double x = _camTarget.X + _camDistance * Math.Cos(_camElevation) * Math.Cos(_camAzimuth);
            double z = _camTarget.Z + _camDistance * Math.Cos(_camElevation) * Math.Sin(_camAzimuth);
            double y = _camTarget.Y + _camDistance * Math.Sin(_camElevation);

            var pos = new Point3D(x, y, z);
            MainCamera.Position = pos;
            MainCamera.LookDirection = new Vector3D(_camTarget.X - pos.X, _camTarget.Y - pos.Y, _camTarget.Z - pos.Z);
            MainCamera.UpDirection = new Vector3D(0, 1, 0);

            UpdateLabels();
        }

        // Slider zoom changed by user
        private void SliderZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update camera distance and refresh
            _camDistance = SliderZoom.Value;
            UpdateCamera();
        }

        // Mouse handlers (rotate/pan/zoom)
        private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePos = e.GetPosition(this);
            if (e.ChangedButton == MouseButton.Left)
            {
                _isLeftDown = true;
                Mouse.Capture(Viewport);
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                _isMiddleDown = true;
                Mouse.Capture(Viewport);
            }
        }

        private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isLeftDown = false;
                Mouse.Capture(null);
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                _isMiddleDown = false;
                Mouse.Capture(null);
            }
        }

        private void Viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            _isLeftDown = false;
            _isMiddleDown = false;
            Mouse.Capture(null);
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            var dx = p.X - _lastMousePos.X;
            var dy = p.Y - _lastMousePos.Y;
            _lastMousePos = p;

            if (_isLeftDown)
            {
                // rotate: change azimuth/elevation
                _camAzimuth -= dx * 0.01;
                _camElevation += dy * 0.01;
                UpdateCamera();
            }
            else if (_isMiddleDown)
            {
                // pan: move target in camera local plane
                var right = Vector3D.CrossProduct(MainCamera.LookDirection, MainCamera.UpDirection);
                right.Normalize();
                var up = MainCamera.UpDirection;
                var factor = _camDistance * 0.0015;
                _camTarget = new Point3D(
                    _camTarget.X - right.X * dx * factor + up.X * dy * factor,
                    _camTarget.Y - right.Y * dx * factor + up.Y * dy * factor,
                    _camTarget.Z - right.Z * dx * factor + up.Z * dy * factor
                );
                UpdateCamera();
            }
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta = e.Delta > 0 ? 0.85 : 1.15;
            _camDistance = Math.Max(0.4, Math.Min(50.0, _camDistance * delta));
            // mirror change to slider
            SliderZoom.Value = _camDistance;
            UpdateCamera();
        }

        // --- Geometry helpers (box, sphere, tube approximations) ---
        private GeometryModel3D CreateBoxModel(Point3D center, double lengthX, double lengthY, double lengthZ, Color color)
        {
            double hx = lengthX / 2, hy = lengthY / 2, hz = lengthZ / 2;
            var p0 = new Point3D(center.X - hx, center.Y - hy, center.Z - hz);
            var p1 = new Point3D(center.X + hx, center.Y - hy, center.Z - hz);
            var p2 = new Point3D(center.X + hx, center.Y + hy, center.Z - hz);
            var p3 = new Point3D(center.X - hx, center.Y + hy, center.Z - hz);
            var p4 = new Point3D(center.X - hx, center.Y - hy, center.Z + hz);
            var p5 = new Point3D(center.X + hx, center.Y - hy, center.Z + hz);
            var p6 = new Point3D(center.X + hx, center.Y + hy, center.Z + hz);
            var p7 = new Point3D(center.X - hx, center.Y + hy, center.Z + hz);

            var mesh = new MeshGeometry3D();

            void AddTriangle(Point3D a, Point3D b, Point3D c)
            {
                int i = mesh.Positions.Count;
                mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
            }

            AddTriangle(p3, p2, p1); AddTriangle(p3, p1, p0);
            AddTriangle(p4, p5, p6); AddTriangle(p4, p6, p7);
            AddTriangle(p0, p1, p5); AddTriangle(p0, p5, p4);
            AddTriangle(p2, p3, p7); AddTriangle(p2, p7, p6);
            AddTriangle(p1, p2, p6); AddTriangle(p1, p6, p5);
            AddTriangle(p3, p0, p4); AddTriangle(p3, p4, p7);

            var mat = new DiffuseMaterial(new SolidColorBrush(color));
            return new GeometryModel3D(mesh, mat);
        }

        private GeometryModel3D CreateSphereModel(Point3D center, double radius, Color color, int tDiv = 16, int pDiv = 12)
        {
            var mesh = new MeshGeometry3D();
            for (int pi = 0; pi <= pDiv; pi++)
            {
                double phi = Math.PI * pi / pDiv;
                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double theta = 2 * Math.PI * ti / tDiv;
                    double x = Math.Sin(phi) * Math.Cos(theta);
                    double y = Math.Cos(phi);
                    double z = Math.Sin(phi) * Math.Sin(theta);
                    mesh.Positions.Add(new Point3D(center.X + radius * x, center.Y + radius * y, center.Z + radius * z));
                }
            }

            for (int pi = 0; pi < pDiv; pi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int a = pi * (tDiv + 1) + ti;
                    int b = a + tDiv + 1;
                    int c = a + 1;
                    int d = b + 1;
                    mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(c);
                    mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(d);
                }
            }
            var mat = new DiffuseMaterial(new SolidColorBrush(color));
            return new GeometryModel3D(mesh, mat);
        }

        // Thin tube implemented as a thin box between points (approx)
        private GeometryModel3D CreateTube(Point3D a, Point3D b, double thickness, Color color)
        {
            var v = new Vector3D(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            double len = v.Length;
            if (len < 1e-6) len = 1e-6;
            var center = new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
            return CreateBoxModel(center, len, thickness, thickness, color);
        }

        private Model3DGroup CreateAxes(double length = 1.6, double thickness = 0.008)
        {
            var grp = new Model3DGroup();
            // X axis (red)
            grp.Children.Add(CreateTube(new Point3D(-0.2, -0.3, -0.3), new Point3D(-0.2 + length, -0.3, -0.3), thickness, Colors.Red));
            // Y axis (green)
            grp.Children.Add(CreateTube(new Point3D(-0.2, -0.3, -0.3), new Point3D(-0.2, -0.3 + length, -0.3), thickness, Colors.Green));
            // Z axis (blue)
            grp.Children.Add(CreateTube(new Point3D(-0.2, -0.3, -0.3), new Point3D(-0.2, -0.3, -0.3 + length), thickness, Colors.Blue));
            return grp;
        }

        // Helper class to store sensor info and label element reference
        private class SensorMarker
        {
            public string Name;
            public Point3D Position;
            public Color Color;
            public Vector LabelOffset = new Vector(40, -30); // default pixel offset for label
            public FrameworkElement LabelElement;
            public Line ConnectorLine;
            public Polygon ConnectorArrow;
        }
    }
}