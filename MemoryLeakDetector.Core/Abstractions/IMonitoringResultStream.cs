using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IMonitoringResultStream
{
    ValueTask PublishAsync(MonitoringResultDto result, CancellationToken cancellationToken);
    IAsyncEnumerable<MonitoringResultDto> ReadAllAsync(CancellationToken cancellationToken);
}

