using System.Text;
using System.Text.Json;
using LSM6DSOX.Visualizer.Models;
using NATS.Client;
using NATS.Client.JetStream;

namespace LSM6DSOX.Visualizer;

/// <summary>
/// Prenumererar på IMU-sensorns subject. Använder en ordnad JetStream-konsument som bara
/// tar nya meddelanden (att spela upp gammal IMU-data skulle bara snurra orienteringsfiltret)
/// och faller tillbaka på en vanlig core-NATS-prenumeration om ingen JetStream finns.
/// </summary>
public class NatsImuSubscriber : IDisposable
{
    private const string SensorType = "lsm6dsox";

    private readonly string _url;
    private readonly string _subject;
    private IConnection? _connection;
    private IAsyncSubscription? _subscription;

    public event Action<ImuReading>? ReadingReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? ConnectionError;

    public NatsImuSubscriber(string url, string subject)
    {
        _url = url;
        _subject = subject;
    }

    public void Start()
    {
        try
        {
            var opts = ConnectionFactory.GetDefaultOptions();
            opts.Url = _url;
            opts.Name = "lsm6dsox-visualizer";
            opts.MaxReconnect = Options.ReconnectForever;
            opts.ReconnectWait = 2000;
            opts.DisconnectedEventHandler += (_, _) => StatusChanged?.Invoke("Frånkopplad, försöker återansluta...");
            opts.ReconnectedEventHandler += (_, _) => StatusChanged?.Invoke("Återansluten");
            opts.ClosedEventHandler += (_, _) => StatusChanged?.Invoke("Anslutningen stängd");
            opts.AsyncErrorEventHandler += (_, args) => ConnectionError?.Invoke(new InvalidOperationException(args.Error));

            _connection = new ConnectionFactory().CreateConnection(opts);

            try
            {
                var js = _connection.CreateJetStreamContext();
                var consumerConfig = ConsumerConfiguration.Builder()
                    .WithDeliverPolicy(DeliverPolicy.New)
                    .Build();
                var pso = PushSubscribeOptions.Builder()
                    .WithOrdered(true)
                    .WithConfiguration(consumerConfig)
                    .Build();

                _subscription = js.PushSubscribeAsync(_subject, OnMessage, false, pso);
                StatusChanged?.Invoke($"Ansluten via JetStream, väntar på data ({_subject})...");
            }
            catch (Exception jsEx)
            {
                // Ingen stream för subjectet – kör live utan JetStream
                _subscription = _connection.SubscribeAsync(_subject, OnMessage);
                StatusChanged?.Invoke($"Ansluten utan JetStream – bara live-data ({jsEx.Message})");
            }
        }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(ex);
        }
    }

    private void OnMessage(object? sender, MsgHandlerEventArgs args)
    {
        try
        {
            var msg = args.Message;
            var timestamp = DateTime.Now;

            if (msg.IsJetStream)
            {
                // Serverns lagringstid (UTC) – samma tidsbas som Barometer.Visualizer
                var ts = msg.MetaData.Timestamp;
                timestamp = ts.Kind == DateTimeKind.Local
                    ? ts
                    : DateTime.SpecifyKind(ts, DateTimeKind.Utc).ToLocalTime();
            }

            var reading = Parse(Encoding.UTF8.GetString(msg.Data), timestamp);
            if (reading is not null)
            {
                ReadingReceived?.Invoke(reading);
            }
        }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Packar upp kuvertet. "value" är en JSON-sträng i dagens firmware,
    /// men ett inbäddat objekt accepteras också.
    /// </summary>
    internal static ImuReading? Parse(string json, DateTime timestamp)
    {
        using var envelope = JsonDocument.Parse(json);
        var root = envelope.RootElement;

        if (root.TryGetProperty("type", out var type) && type.GetString() != SensorType)
            return null;

        var senderId = root.TryGetProperty("sender", out var s) ? s.GetString() ?? "" : "";

        if (!root.TryGetProperty("value", out var value))
            return null;

        JsonDocument? inner = null;
        try
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                inner = JsonDocument.Parse(value.GetString()!);
                value = inner.RootElement;
            }

            return new ImuReading(
                timestamp,
                value.GetProperty("t").GetInt64(),
                value.GetProperty("ax").GetDouble(),
                value.GetProperty("ay").GetDouble(),
                value.GetProperty("az").GetDouble(),
                value.GetProperty("gx").GetDouble(),
                value.GetProperty("gy").GetDouble(),
                value.GetProperty("gz").GetDouble(),
                senderId);
        }
        finally
        {
            inner?.Dispose();
        }
    }

    public void Dispose()
    {
        try
        {
            _subscription?.Unsubscribe();
            _connection?.Close();
            _connection?.Dispose();
        }
        catch
        {
            // Stänger ner ändå
        }
    }
}
