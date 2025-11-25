using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IGcMetricsProvider
{
    GcMetrics? TryCollect(int processId, string processName);
}

