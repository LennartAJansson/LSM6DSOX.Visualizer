using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using LSM6DSOX.Visualizer.Models;
using ScottPlot.WPF;
using SpColor = ScottPlot.Color;

namespace LSM6DSOX.Visualizer;

public partial class MainWindow : Window
{
    // --- Justera efter din uppsättning ---
    private const string NatsUrl = "nats://192.168.50.254:4222";
    private const string NatsSubject = "sensors.lsm6dsox.raw"; // Subjects är skiftlägeskänsliga – gemener, till skillnad från streamen SENSORS
    private const int MaxPoints = 20_000; // ~1 timme vid 5 Hz

    // Längre uppehåll mellan två sampel (t.ex. efter en omstart av ESP32) integreras inte i filtret.
    private const double MaxFilterStepSeconds = 1.0;
    private const double ArrowScale = 2.0; // vid vila är |a| ~1 g, vilket annars blir en kort pil

    private static readonly SpColor AxisX = SpColor.FromHex("#FF7043");
    private static readonly SpColor AxisY = SpColor.FromHex("#81C784");
    private static readonly SpColor AxisZ = SpColor.FromHex("#4FC3F7");

    private readonly ConcurrentQueue<ImuReading> _incoming = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly MadgwickAhrs _filter = new();
    private readonly List<double> _times = new(); // OADate, delas av alla serier
    private readonly ChartSeries _accel;
    private readonly ChartSeries _gyro;
    private readonly List<double> _ax, _ay, _az, _gx, _gy, _gz;

    private NatsImuSubscriber? _subscriber;
    private TimeSpan? _window = TimeSpan.FromMinutes(1); // null = allt
    private long _sampleCount;
    private long? _lastDeviceTimeMs;
    private DateTime _lastRefresh = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        BuildCubeGeometry();

        _accel = new ChartSeries(AccelPlot, "Acceleration", "g", minSpan: 0.5);
        _ax = _accel.AddLine(AxisX);
        _ay = _accel.AddLine(AxisY);
        _az = _accel.AddLine(AxisZ);

        _gyro = new ChartSeries(GyroPlot, "Vinkelhastighet", "°/s", minSpan: 20.0);
        _gx = _gyro.AddLine(AxisX);
        _gy = _gyro.AddLine(AxisY);
        _gz = _gyro.AddLine(AxisZ);

        // Töm kön i klump några gånger per sekund i stället för per meddelande.
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _uiTimer.Tick += (_, _) => DrainQueue();

        Loaded += (_, _) => StartNats();
        Closing += (_, _) =>
        {
            _uiTimer.Stop();
            _subscriber?.Dispose();
        };
    }

    private void BuildCubeGeometry()
    {
        var builder = new MeshBuilder();
        // Kub som representerar sensorbrädet: bredare i X/Y, tunnare i Z
        builder.AddBox(new Point3D(0, 0, 0), 1.2, 0.8, 0.2);
        CubeGeometry.Geometry = builder.ToMesh();
    }

    private void StartNats()
    {
        _subscriber = new NatsImuSubscriber(NatsUrl, NatsSubject);
        _subscriber.ReadingReceived += r => _incoming.Enqueue(r);
        _subscriber.StatusChanged += s => Dispatcher.InvokeAsync(() => SetStatus(Brushes.Orange, s));
        _subscriber.ConnectionError += ex => Dispatcher.InvokeAsync(() => SetStatus(Brushes.Red, $"Fel: {ex.Message}"));

        _uiTimer.Start();
        // Anslutningen blockerar – kör den utanför UI-tråden
        Task.Run(_subscriber.Start);
    }

    private void DrainQueue()
    {
        ImuReading? latest = null;

        while (_incoming.TryDequeue(out var r))
        {
            _times.Add(r.Timestamp.ToOADate());
            _ax.Add(r.AccelX);
            _ay.Add(r.AccelY);
            _az.Add(r.AccelZ);
            _gx.Add(r.GyroX);
            _gy.Add(r.GyroY);
            _gz.Add(r.GyroZ);

            UpdateFilter(r);
            latest = r;
            _sampleCount++;
        }

        if (latest is not null)
        {
            ShowLatest(latest);
        }
        // Rita bara om vid nytt data, eller varje sekund så att tidsfönstret glider fram.
        // (Då går det också att zooma/panorera med musen mellan uppdateringarna.)
        else if (DateTime.Now - _lastRefresh < TimeSpan.FromSeconds(1))
        {
            return;
        }

        TrimOldPoints();
        RefreshPlots();
    }

    private void UpdateFilter(ImuReading r)
    {
        // Tidssteget kommer från ESP32:ns egen klocka, inte från nätverkets ankomsttider.
        if (_lastDeviceTimeMs is { } last)
        {
            var dt = (r.DeviceTimeMs - last) / 1000.0;
            if (dt > 0 && dt <= MaxFilterStepSeconds)
            {
                _filter.UpdateImu(r.GyroX, r.GyroY, r.GyroZ, r.AccelX, r.AccelY, r.AccelZ, dt);
            }
        }
        _lastDeviceTimeMs = r.DeviceTimeMs;
    }

    private void ShowLatest(ImuReading latest)
    {
        CubeRotation.Quaternion = new Quaternion(_filter.Q1, _filter.Q2, _filter.Q3, _filter.Q0);
        AccelArrow.Point2 = new Point3D(latest.AccelX * ArrowScale, latest.AccelY * ArrowScale, latest.AccelZ * ArrowScale);

        var accelMagnitude = Math.Sqrt(latest.AccelX * latest.AccelX + latest.AccelY * latest.AccelY + latest.AccelZ * latest.AccelZ);
        var gyroMagnitude = Math.Sqrt(latest.GyroX * latest.GyroX + latest.GyroY * latest.GyroY + latest.GyroZ * latest.GyroZ);
        var (roll, pitch, _) = _filter.GetEulerDegrees();

        AccelValue.Text = $"{accelMagnitude:F2} g";
        GyroValue.Text = $"{gyroMagnitude:F1} °/s";
        RollValue.Text = $"{roll:F1} °";
        PitchValue.Text = $"{pitch:F1} °";
        LastUpdateText.Text = $"Senast: {latest.Timestamp:yyyy-MM-dd HH:mm:ss}";
        SampleCountText.Text = $"Mätvärden: {_sampleCount}";
        SenderText.Text = latest.Sender;

        if (StatusDot.Fill != Brushes.Red)
            StatusDot.Fill = Brushes.LimeGreen;
    }

    private void TrimOldPoints()
    {
        var excess = _times.Count - MaxPoints;
        if (excess <= 0) return;

        _times.RemoveRange(0, excess);
        foreach (var values in new[] { _ax, _ay, _az, _gx, _gy, _gz })
        {
            values.RemoveRange(0, excess);
        }
    }

    private void RefreshPlots()
    {
        _lastRefresh = DateTime.Now;
        if (_times.Count == 0) return;

        var right = DateTime.Now.ToOADate();
        var left = _window is { } w
            ? DateTime.Now.Subtract(w).ToOADate()
            : _times[0];

        // Lite luft på höger sida så att senaste punkten inte hamnar i kanten
        var pad = (right - left) * 0.01;

        // Första index som ligger inom tidsfönstret (listan är sorterad)
        var first = _times.BinarySearch(left);
        if (first < 0) first = ~first;

        foreach (var series in new[] { _accel, _gyro })
        {
            series.EnsureScatter(_times);
            series.SetLimits(left, right + pad, first);
        }
    }

    private void WindowSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowSelector.SelectedItem is not ComboBoxItem { Tag: string tag }) return;

        var seconds = int.Parse(tag);
        _window = seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;

        // Anropas även under InitializeComponent, innan serierna finns
        if (_accel is not null)
            RefreshPlots();
    }

    private void SetStatus(Brush color, string text)
    {
        StatusDot.Fill = color;
        StatusText.Text = text;
    }

    /// <summary>
    /// Ett diagram (WpfPlot) med en eller flera tidsserier och mörkt tema som matchar Barometer-visualiseraren.
    /// Förklaringen till linjefärgerna ligger som text i XAML så att den följer samma tema.
    /// </summary>
    private sealed class ChartSeries
    {
        private readonly WpfPlot _plot;
        private readonly double _minSpan;
        private readonly List<(List<double> Values, SpColor Color)> _lines = new();
        private bool _created;

        public ChartSeries(WpfPlot plot, string title, string unit, double minSpan)
        {
            _plot = plot;
            _minSpan = minSpan;

            var p = plot.Plot;
            p.FigureBackground.Color = SpColor.FromHex("#1E1E1E");
            p.DataBackground.Color = SpColor.FromHex("#252526");
            p.Axes.Color(SpColor.FromHex("#BBBBBB"));
            p.Grid.MajorLineColor = SpColor.FromHex("#3A3A3A");
            p.Axes.DateTimeTicksBottom();
            p.YLabel($"{title} ({unit})");
            plot.Refresh();
        }

        /// <summary>Lägger till en linje och returnerar listan som värdena ska skrivas till.</summary>
        public List<double> AddLine(SpColor color)
        {
            var values = new List<double>();
            _lines.Add((values, color));
            return values;
        }

        /// <summary>Skapas först när det finns data – ScottPlot gillar inte tomma serier.</summary>
        public void EnsureScatter(List<double> times)
        {
            if (_created) return;
            _created = true;

            foreach (var (values, color) in _lines)
            {
                var scatter = _plot.Plot.Add.Scatter(times, values);
                scatter.Color = color;
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;
            }
        }

        public void SetLimits(double left, double right, int firstVisible)
        {
            // Y-skala efter de punkter som syns i fönstret, över alla linjer
            double min = double.MaxValue, max = double.MinValue;
            foreach (var (values, _) in _lines)
            {
                for (var i = firstVisible; i < values.Count; i++)
                {
                    min = Math.Min(min, values[i]);
                    max = Math.Max(max, values[i]);
                }
            }

            if (min > max)
            {
                // Inget i fönstret – visa senaste värdena
                foreach (var (values, _) in _lines)
                {
                    min = Math.Min(min, values[^1]);
                    max = Math.Max(max, values[^1]);
                }
            }

            var span = Math.Max(max - min, _minSpan);
            var mid = (max + min) / 2;
            var bottom = mid - span * 0.6;
            var top = mid + span * 0.6;

            _plot.Plot.Axes.SetLimits(left, right, bottom, top);
            _plot.Refresh();
        }
    }
}
