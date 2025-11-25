namespace MemoryLeakDetector.Core.Models;

public sealed class ProcessMetricSnapshot
{
    public ProcessMetricSnapshot(
        int processId,
        string processName,
        double workingSetMb,
        double virtualMemoryMb,
        int handleCount,
        DateTime capturedAtUtc)
    {
        ProcessId = processId;
        ProcessName = processName;
        WorkingSetMb = workingSetMb;
        VirtualMemoryMb = virtualMemoryMb;
        HandleCount = handleCount;
        CapturedAtUtc = capturedAtUtc;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public double WorkingSetMb { get; }
    public double VirtualMemoryMb { get; }
    public int HandleCount { get; }
    public DateTime CapturedAtUtc { get; }
}

