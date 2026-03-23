using System.Collections.Concurrent;
using System.Text.Json;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AutoNomX.Infrastructure.EventBus;

public class PostgresEventBus : IEventBus, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _handlers = new();
    private NpgsqlConnection? _listenerConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _isListening;

    public PostgresEventBus(IConfiguration configuration, ILogger<PostgresEventBus> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string is required.");
        _logger = logger;
    }

    public async Task PublishAsync(string channel, string payload, CancellationToken ct = default)
    {
        var sanitizedChannel = SanitizeChannel(channel);
        var escapedPayload = payload.Replace("'", "''");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand($"NOTIFY {sanitizedChannel}, '{escapedPayload}'", connection);
        await cmd.ExecuteNonQueryAsync(ct);

        _logger.LogDebug("Published to channel {Channel}: {PayloadLength} chars", sanitizedChannel, payload.Length);
    }

    public async Task SubscribeAsync(string channel, Func<string, Task> handler, CancellationToken ct = default)
    {
        var sanitizedChannel = SanitizeChannel(channel);

        _handlers.AddOrUpdate(
            sanitizedChannel,
            [handler],
            (_, existing) => { existing.Add(handler); return existing; });

        await EnsureListeningAsync(sanitizedChannel, ct);

        _logger.LogDebug("Subscribed to channel {Channel}", sanitizedChannel);
    }

    private async Task EnsureListeningAsync(string channel, CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_listenerConnection is null || _listenerConnection.State != System.Data.ConnectionState.Open)
            {
                _listenerConnection?.Dispose();
                _listenerConnection = new NpgsqlConnection(_connectionString);
                await _listenerConnection.OpenAsync(ct);

                _listenerConnection.Notification += OnNotification;
                _isListening = false;
            }

            await using var cmd = new NpgsqlCommand($"LISTEN {channel}", _listenerConnection);
            await cmd.ExecuteNonQueryAsync(ct);

            if (!_isListening)
            {
                _isListening = true;
                _ = ListenLoopAsync(ct);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _listenerConnection is not null)
            {
                await _listenerConnection.WaitAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EventBus listen loop error, will attempt to reconnect");
        }
    }

    private void OnNotification(object sender, NpgsqlNotificationEventArgs e)
    {
        if (!_handlers.TryGetValue(e.Channel, out var handlers))
            return;

        foreach (var handler in handlers.ToList())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await handler(e.Payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling notification on channel {Channel}", e.Channel);
                }
            });
        }
    }

    private static string SanitizeChannel(string channel)
    {
        // PostgreSQL channel names: only alphanumeric and underscores
        return new string(channel.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()).ToLowerInvariant();
    }

    public async ValueTask DisposeAsync()
    {
        if (_listenerConnection is not null)
        {
            await _listenerConnection.DisposeAsync();
            _listenerConnection = null;
        }

        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
