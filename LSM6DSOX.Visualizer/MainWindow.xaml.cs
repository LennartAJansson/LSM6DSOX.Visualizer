using System;
using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LSM6DSOX.Visualizer.Models;

namespace LSM6DSOX.Visualizer;

public partial class MainWindow : Window
{
    // --- Justera efter din uppsättning ---
    private const string NatsUrl = "nats://192.168.50.47:4222";
    private const string NatsSubject = "sensors.lsm6dsox.raw";
    private const double SampleRateHz = 5.0; // ESP32 skickar var 200:e ms => ~5 Hz

    private readonly MadgwickAhrs _filter = new(SampleRateHz);
    private NatsImuSubscriber? _subscriber;
    private long _sampleCount;

    public MainWindow()
    {
        InitializeComponent();
        BuildCubeGeometry();
        Loaded += (_, _) => StartNats();
        Closing += (_, _) => _subscriber?.Dispose();
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
        _subscriber.SampleReceived += OnSampleReceived;
        _subscriber.ConnectionError += OnConnectionError;
        _subscriber.Start();

        SetStatus(Brushes.Orange, $"Ansluten, väntar på data ({NatsSubject})...");
    }

    private void OnSampleReceived(ImuSample sample)
    {
        // Madgwick förväntar sig konsekvent enhet för accel (normaliseras internt),
        // och gyro i grader/sekund - vilket matchar precis vad ESP32 skickar.
        _filter.UpdateImu(
            sample.GyroX, sample.GyroY, sample.GyroZ,
            sample.AccelX, sample.AccelY, sample.AccelZ);

        var quaternion = new Quaternion(_filter.Q1, _filter.Q2, _filter.Q3, _filter.Q0);
        _sampleCount++;

        // Skala upp för synlighet - vid vila är |a| ~1g, vilket annars blir en kort pil
        const double arrowScale = 2.0;
        var accelPoint = new Point3D(
            sample.AccelX * arrowScale,
            sample.AccelY * arrowScale,
            sample.AccelZ * arrowScale);

        Dispatcher.Invoke(() =>
        {
            CubeRotation.Quaternion = quaternion;
            AccelArrow.Point2 = accelPoint;
            SetStatus(Brushes.LimeGreen, "Tar emot data");
            SampleCountText.Text = $"Sampel mottagna: {_sampleCount}";
        });
    }

    private void OnConnectionError(Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            SetStatus(Brushes.Red, $"Fel: {ex.Message}");
        });
    }

    private void SetStatus(Brush color, string text)
    {
        StatusDot.Fill = color;
        StatusText.Text = text;
    }
}
