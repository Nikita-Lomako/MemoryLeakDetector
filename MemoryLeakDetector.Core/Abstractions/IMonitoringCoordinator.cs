using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IMonitoringCoordinator
{
    Task<MonitoringCycleResult> RunCycleAsync(CancellationToken cancellationToken);
}

