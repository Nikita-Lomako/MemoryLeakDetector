using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.UI.Services.Monitoring
{
    public sealed class NamedPipeMonitoringResultSubscriber : IMonitoringResultSubscriber
    {
        private readonly MonitoringPipeOptions _options;
        private readonly ILogger<NamedPipeMonitoringResultSubscriber> _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public NamedPipeMonitoringResultSubscriber(
            IOptions<MonitoringPipeOptions> options,
            ILogger<NamedPipeMonitoringResultSubscriber> logger)
        {
            _options = options.Value;
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async IAsyncEnumerable<MonitoringResultDto> ListenAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var channel = Channel.CreateBounded<MonitoringResultDto>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Start the listening task
            var listeningTask = Task.Run(async () => await ListenAndSendToChannelAsync(channel.Writer, linkedCts.Token), linkedCts.Token);

            try
            {
                await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return result;
                }
            }
            finally
            {
                linkedCts.Cancel();
                await listeningTask.ConfigureAwait(false);
            }
        }

        private async Task ListenAndSendToChannelAsync(ChannelWriter<MonitoringResultDto> writer, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipe = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Connected to monitoring pipe {Pipe}", _options.PipeName);

                    await ReadLoopAsync(pipe, writer, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Monitoring pipe {Pipe} disconnected, attempting to reconnect...", _options.PipeName);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex, "Timed out connecting to monitoring pipe {Pipe}", _options.PipeName);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Monitoring pipe {Pipe} encountered an IO error", _options.PipeName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected monitoring pipe failure");
                }

                await DelayReconnectAsync(cancellationToken).ConfigureAwait(false);
            }

            writer.Complete();
        }

        private async Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken)
        {
            var pipe = new NamedPipeClientStream(
                ".",
                _options.PipeName,
                PipeDirection.In,
                PipeOptions.Asynchronous);

            var timeout = _options.ClientConnectTimeoutMs <= 0 ? Timeout.Infinite : _options.ClientConnectTimeoutMs;
            await pipe.ConnectAsync(timeout, cancellationToken).ConfigureAwait(false);
            TrySetReadMode(pipe);

            return pipe;
        }

        private async Task ReadLoopAsync(NamedPipeClientStream pipe, ChannelWriter<MonitoringResultDto> writer, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);

            while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var result = JsonSerializer.Deserialize<MonitoringResultDto>(line, _serializerOptions);
                    if (result is not null)
                    {
                        await writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Discarding malformed monitoring payload");
                }
            }
        }

        private void TrySetReadMode(NamedPipeClientStream pipe)
        {
            try
            {
                if (pipe.CanRead && pipe.ReadMode != PipeTransmissionMode.Byte)
                {
                    pipe.ReadMode = PipeTransmissionMode.Byte;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Insufficient permissions to adjust read mode for pipe {Pipe}", _options.PipeName);
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Failed to adjust read mode for pipe {Pipe}", _options.PipeName);
            }
        }

        private Task DelayReconnectAsync(CancellationToken cancellationToken)
        {
            if (_options.ReconnectDelayMilliseconds <= 0)
            {
                return Task.CompletedTask;
            }

            return Task.Delay(TimeSpan.FromMilliseconds(_options.ReconnectDelayMilliseconds), cancellationToken);
        }
    }
}