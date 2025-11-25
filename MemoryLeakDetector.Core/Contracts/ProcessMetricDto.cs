namespace MemoryLeakDetector.Core.Contracts;

public sealed class ProcessMetricDto
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public double WorkingSetMb { get; init; }
    public double VirtualMemoryMb { get; init; }
    public int HandleCount { get; init; }
    public DateTime CapturedAtUtc { get; init; }
    public double? CpuUsagePercent { get; init; }
    public double? GcHeapSizeMb { get; init; }
    public double? LargeObjectHeapMb { get; init; }
    public double? Gen0CollectionsPerSec { get; init; }
    public double? Gen2CollectionsPerSec { get; init; }
}

