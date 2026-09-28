using System.Threading.Channels;
using Domolov.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Domolov.Infrastructure.Signals;

/// <summary>Publishes signals with PostgreSQL <c>pg_notify</c>.</summary>
public sealed class PostgresSignalPublisher(NpgsqlDataSource dataSource) : ISignalPublisher
{
    public async Task PublishAsync(
        string channel,
        string payload,
        CancellationToken cancellationToken = default
    )
    {
        await using var cmd = dataSource.CreateCommand("SELECT pg_notify($1, $2)");
        cmd.Parameters.AddWithValue(channel);
        cmd.Parameters.AddWithValue(payload);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>A received signal.</summary>
public sealed record Signal(string Channel, string Payload);

/// <summary>In-process fan-out of received signals to local subscribers.</summary>
public sealed class SignalBus
{
    private readonly Lock _gate = new();
    private readonly List<Channel<Signal>> _subscribers = [];

    /// <summary>Subscribes until the token is cancelled.</summary>
    public async IAsyncEnumerable<Signal> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var channel = Channel.CreateBounded<Signal>(
            new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest }
        );
        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        try
        {
            await foreach (var signal in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return signal;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    public void Publish(Signal signal)
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(signal);
            }
        }
    }
}

/// <summary>
/// Holds a dedicated connection that LISTENs on every signal channel and republishes
/// notifications on the <see cref="SignalBus"/>. Reconnects with backoff.
/// </summary>
public sealed class PostgresSignalListener(
    NpgsqlDataSource dataSource,
    SignalBus bus,
    ILogger<PostgresSignalListener> logger
) : BackgroundService
{
    private static readonly string[] Channels =
    [
        SignalChannels.ScanRequested,
        SignalChannels.ScanRunChanged,
        SignalChannels.CleanupRequested,
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await dataSource.OpenConnectionAsync(stoppingToken);
                connection.Notification += (_, e) => bus.Publish(new Signal(e.Channel, e.Payload));
                foreach (var channel in Channels)
                {
                    await using var cmd = new NpgsqlCommand($"LISTEN {channel}", connection);
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation(
                    "Listening for signals on {Channels}",
                    string.Join(", ", Channels)
                );
                delay = TimeSpan.FromSeconds(1);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Signal listener lost its connection; retrying in {Delay}",
                    delay
                );
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
            }
        }
    }
}
