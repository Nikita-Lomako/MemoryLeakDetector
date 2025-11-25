using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace MemoryLeakDetector.Core.Services.Streaming;

public sealed class InMemoryMonitoringResultStream : IMonitoringResultStream
{
    private readonly Channel<MonitoringResultDto> _channel;
    private readonly ILogger<InMemoryMonitoringResultStream> _logger;

    public InMemoryMonitoringResultStream(ILogger<InMemoryMonitoringResultStream> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<MonitoringResultDto>(new BoundedChannelOptions(500)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public async ValueTask PublishAsync(MonitoringResultDto result, CancellationToken cancellationToken)
    {
        while (await _channel.Writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_channel.Writer.TryWrite(result))
            {
                return;
            }
        }

        _logger.LogWarning("Failed to publish monitoring result — channel unavailable");
    }

    public async IAsyncEnumerable<MonitoringResultDto> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }
}

