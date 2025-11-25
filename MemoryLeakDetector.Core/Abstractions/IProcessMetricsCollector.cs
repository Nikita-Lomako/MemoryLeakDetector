using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IProcessMetricsCollector
{
    Task<IReadOnlyCollection<ProcessMetricSnapshot>> CollectAsync(CancellationToken cancellationToken);
}

