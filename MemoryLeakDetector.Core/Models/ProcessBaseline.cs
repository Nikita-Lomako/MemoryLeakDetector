namespace MemoryLeakDetector.Core.Models;

public sealed class ProcessBaseline
{
    public ProcessBaseline(
        int processId,
        string processName,
        double averageWorkingSetMb,
        double averageVirtualMemoryMb,
        double averageHandleCount,
        int sampleCount,
        DateTime lastUpdatedUtc)
    {
        ProcessId = processId;
        ProcessName = processName;
        AverageWorkingSetMb = averageWorkingSetMb;
        AverageVirtualMemoryMb = averageVirtualMemoryMb;
        AverageHandleCount = averageHandleCount;
        SampleCount = sampleCount;
        LastUpdatedUtc = lastUpdatedUtc;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public double AverageWorkingSetMb { get; }
    public double AverageVirtualMemoryMb { get; }
    public double AverageHandleCount { get; }
    public int SampleCount { get; }
    public DateTime LastUpdatedUtc { get; }
}

