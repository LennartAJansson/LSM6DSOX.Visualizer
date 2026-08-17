using System;
using System.Text;
using System.Text.Json;
using LSM6DSOX.Visualizer.Models;
using NATS.Client;

namespace LSM6DSOX.Visualizer;

/// <summary>
/// Prenumererar på ESP32:ns rådata-subject och skickar vidare
/// deserialiserade sampel via ett event.
/// </summary>
public class NatsImuSubscriber : IDisposable
{
    private readonly string _url;
    private readonly string _subject;
    private IConnection? _connection;
    private IAsyncSubscription? _subscription;

    public event Action<ImuSample>? SampleReceived;
    public event Action<Exception>? ConnectionError;
    public event Action? Connected;
    public event Action? Disconnected;

    public NatsImuSubscriber(string url, string subject)
    {
        _url = url;
        _subject = subject;
    }

    public void Start()
    {
        try
        {
            var cf = new ConnectionFactory();
            var opts = ConnectionFactory.GetDefaultOptions();
            opts.Url = _url;
            opts.MaxReconnect = Options.ReconnectForever;
            opts.ReconnectWait = 2000;
            opts.DisconnectedEventHandler = (_, _) => Disconnected?.Invoke();
            opts.ReconnectedEventHandler = (_, _) => Connected?.Invoke();
            opts.ClosedEventHandler = (_, _) => Disconnected?.Invoke();
            opts.AsyncErrorEventHandler = (_, args) => ConnectionError?.Invoke(new InvalidOperationException(args.Error));

            _connection = cf.CreateConnection(opts);
            _subscription = _connection.SubscribeAsync(_subject, OnMessage);

            if (_connection.State == ConnState.CONNECTED)
            {
                Connected?.Invoke();
            }
            else
            {
                Disconnected?.Invoke();
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
            var json = Encoding.UTF8.GetString(args.Message.Data);
            var sample = JsonSerializer.Deserialize<ImuSample>(json);
            if (sample is not null)
            {
                SampleReceived?.Invoke(sample);
            }
        }
        catch (Exception ex)
        {
            ConnectionError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        _subscription?.Unsubscribe();
        _connection?.Close();
        _connection?.Dispose();
    }
}
