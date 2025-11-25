namespace MemoryLeakDetector.Core.Contracts;

public sealed class ProcessMetricDto
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public double WorkingSetMb { get; init; }
    public double VirtualMemoryMb { get; init; }
    public int HandleCount { get; init; }
    public DateTime CapturedAtUtc { get; init; }
}

