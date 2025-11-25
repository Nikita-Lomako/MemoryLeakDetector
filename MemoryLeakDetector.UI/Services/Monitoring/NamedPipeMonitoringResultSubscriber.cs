using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

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
                    using var pipe = new NamedPipeClientStream(
                        ".",
                        _options.PipeName,
                        PipeDirection.In,
                        PipeOptions.Asynchronous);

                    await pipe.ConnectAsync(_options.ClientConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Connected to monitoring pipe {Pipe}", _options.PipeName);

                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var result = JsonSerializer.Deserialize<MonitoringResultDto>(line, _serializerOptions);
                        if (result is not null)
                        {
                            await writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read monitoring data, retrying...");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            writer.Complete();
        }
    }
}