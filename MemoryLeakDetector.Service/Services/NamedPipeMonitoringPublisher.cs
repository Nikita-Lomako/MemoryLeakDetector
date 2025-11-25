using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Service.Services;

[SupportedOSPlatform("windows")]
public sealed class NamedPipeMonitoringPublisher : BackgroundService
{
    private readonly IMonitoringResultStream _resultStream;
    private readonly MonitoringPipeOptions _options;
    private readonly ILogger<NamedPipeMonitoringPublisher> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ConcurrentDictionary<int, NamedPipeServerStream> _clients = new();
    private int _clientId;

    public NamedPipeMonitoringPublisher(
        IMonitoringResultStream resultStream,
        IOptions<MonitoringPipeOptions> options,
        ILogger<NamedPipeMonitoringPublisher> logger)
    {
        _resultStream = resultStream;
        _options = options.Value;
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var acceptTask = Task.Run(() => AcceptLoopAsync(stoppingToken), stoppingToken);

        await foreach (var result in _resultStream.ReadAllAsync(stoppingToken))
        {
            var payload = JsonSerializer.Serialize(result, _serializerOptions);
            await BroadcastAsync(payload, stoppingToken);
        }

        await acceptTask;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                _options.PipeName,
                PipeDirection.Out,
                _options.MaxServerConnections,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                var id = Interlocked.Increment(ref _clientId);
                _clients[id] = server;
                _logger.LogInformation("Monitoring client #{ClientId} connected", id);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                server.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipe accept loop error");
                server.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
    }

    private async Task BroadcastAsync(string payload, CancellationToken token)
    {
        var messageBytes = Encoding.UTF8.GetBytes(payload + Environment.NewLine);
        var disconnected = new List<int>();

        foreach (var kvp in _clients)
        {
            var clientId = kvp.Key;
            var stream = kvp.Value;
            if (!stream.IsConnected)
            {
                disconnected.Add(clientId);
                continue;
            }

            try
            {
                await stream.WriteAsync(messageBytes, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast to client #{ClientId}", clientId);
                disconnected.Add(clientId);
                stream.Dispose();
            }
        }

        foreach (var id in disconnected)
        {
            _clients.TryRemove(id, out _);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var stream in _clients.Values)
        {
            try
            {
                stream.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        _clients.Clear();
        return base.StopAsync(cancellationToken);
    }
}

