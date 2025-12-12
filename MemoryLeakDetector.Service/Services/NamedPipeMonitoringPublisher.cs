using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Service.Services;

// Публикует результаты мониторинга через Named Pipe для UI клиентов
[SupportedOSPlatform("windows")]
public sealed class NamedPipeMonitoringPublisher : BackgroundService
{
    private readonly IMonitoringResultStream _resultStream;
    private readonly MonitoringPipeOptions _options;
    private readonly ILogger<NamedPipeMonitoringPublisher> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ConcurrentDictionary<int, PipeClientConnection> _clients = new();
    private readonly SemaphoreSlim _broadcastLock = new(1, 1);
    private readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
        var acceptTask = AcceptLoopAsync(stoppingToken);

        try
        {
            // Читаем результаты и рассылаем клиентам
            await foreach (var result in _resultStream.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await BroadcastAsync(result, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await acceptTask.ConfigureAwait(false);
        }
    }

    // Цикл приема новых подключений
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;

            try
            {
                server = CreatePipeServer();
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                var id = Interlocked.Increment(ref _clientId);
                var connection = new PipeClientConnection(id, server, _utf8NoBom);
                if (!_clients.TryAdd(id, connection))
                {
                    connection.Dispose();
                    continue;
                }

                _logger.LogInformation("Monitoring client #{ClientId} connected", id);
                server = null; // ownership transferred
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipe accept loop error");
                server?.Dispose();
                await DelayAsync(token).ConfigureAwait(false);
            }
        }
    }

    // Рассылка данных всем клиентам
    private async Task BroadcastAsync(MonitoringResultDto result, CancellationToken token)
    {
        // Сериализуем вне lock
        var payload = JsonSerializer.Serialize(result, _serializerOptions);

        // Копируем список клиентов
        var clientsSnapshot = new List<(int Id, PipeClientConnection Connection)>();
        await _broadcastLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            foreach (var (clientId, connection) in _clients)
            {
                clientsSnapshot.Add((clientId, connection));
            }
        }
        finally
        {
            _broadcastLock.Release();
        }

        // Отправляем всем параллельно
        var disconnected = new List<int>();
        var sendTasks = new List<Task>();

        foreach (var (clientId, connection) in clientsSnapshot)
        {
            if (!connection.Stream.IsConnected)
            {
                disconnected.Add(clientId);
                connection.Dispose();
                continue;
            }

            var sendTask = Task.Run(async () =>
            {
                try
                {
                    await connection.Writer.WriteLineAsync(payload).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast to client #{ClientId}", clientId);
                    disconnected.Add(clientId);
                    connection.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    disconnected.Add(clientId);
                }
            }, token);

            sendTasks.Add(sendTask);
        }

        // Ждем с таймаутом
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await Task.WhenAll(sendTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested == false)
        {
            _logger.LogWarning("Broadcast timeout - some clients may not have received the update");
        }

        // Чистим отключенных
        if (disconnected.Count > 0)
        {
            await _broadcastLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                foreach (var id in disconnected)
                {
                    _clients.TryRemove(id, out _);
                }
            }
            finally
            {
                _broadcastLock.Release();
            }
        }
    }

   private NamedPipeServerStream CreatePipeServer()
    {
    PipeSecurity? pipeSecurity = null;
    try
    {
        pipeSecurity = new PipeSecurity();
        var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            users,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to configure pipe security, falling back to defaults");
    }

    return new NamedPipeServerStream(
        _options.PipeName,
        PipeDirection.Out,
        _options.MaxServerConnections,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous,
        0,
        0
    );
    }

    private Task DelayAsync(CancellationToken token)
    {
        if (_options.ReconnectDelayMilliseconds <= 0)
        {
            return Task.CompletedTask;
        }

        return Task.Delay(TimeSpan.FromMilliseconds(_options.ReconnectDelayMilliseconds), token);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var connection in _clients.Values)
        {
            connection.Dispose();
        }

        _clients.Clear();

        return base.StopAsync(cancellationToken);
    }

    // Внутренний класс для подключения клиента
    private sealed class PipeClientConnection : IDisposable
    {
        public PipeClientConnection(int id, NamedPipeServerStream stream, Encoding encoding)
        {
            Id = id;
            Stream = stream;
            Writer = new StreamWriter(stream, encoding, bufferSize: 1024, leaveOpen: false)
            {
                AutoFlush = true
            };
        }

        public int Id { get; }
        public NamedPipeServerStream Stream { get; }
        public StreamWriter Writer { get; }

        public void Dispose()
        {
            try
            {
                Writer.Dispose();
            }
            catch { }

            try
            {
                Stream.Dispose();
            }
            catch { }
        }
    }
}
