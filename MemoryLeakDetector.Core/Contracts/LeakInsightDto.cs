namespace MemoryLeakDetector.Core.Contracts;

public sealed class LeakInsightDto
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public bool IsLeakSuspected { get; init; }
    public double WorkingSetGrowthPercent { get; init; }
    public double WorkingSetDeltaMb { get; init; }
    public double VirtualMemoryGrowthPercent { get; init; }
    public double HandleGrowthPercent { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime BaselineUpdatedAtUtc { get; init; }
    public double BaselineWorkingSetMb { get; init; }
    public double BaselineVirtualMemoryMb { get; init; }
    public double BaselineHandleCount { get; init; }
}

