using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IMonitoringResultMapper
{
    MonitoringResultDto Map(MonitoringCycleResult result);
}

