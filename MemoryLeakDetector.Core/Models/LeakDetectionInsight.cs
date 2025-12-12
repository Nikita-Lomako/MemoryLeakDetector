namespace MemoryLeakDetector.Core.Models;

// Результат анализа процесса на утечку памяти
public sealed class LeakDetectionInsight
{
    public LeakDetectionInsight(
        int processId,
        string processName,
        bool isLeakSuspected,
        double workingSetGrowthPercent,
        double workingSetDeltaMb,
        double virtualMemoryGrowthPercent,
        double handleGrowthPercent,
        string reason,
        ProcessBaseline baseline,
        ProcessMetricSnapshot snapshot)
    {
        ProcessId = processId;
        ProcessName = processName;
        IsLeakSuspected = isLeakSuspected;
        WorkingSetGrowthPercent = workingSetGrowthPercent;
        WorkingSetDeltaMb = workingSetDeltaMb;
        VirtualMemoryGrowthPercent = virtualMemoryGrowthPercent;
        HandleGrowthPercent = handleGrowthPercent;
        Reason = reason;
        Baseline = baseline;
        Snapshot = snapshot;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public bool IsLeakSuspected { get; }
    public double WorkingSetGrowthPercent { get; }
    public double WorkingSetDeltaMb { get; }
    public double VirtualMemoryGrowthPercent { get; }
    public double HandleGrowthPercent { get; }
    public string Reason { get; }
    public ProcessBaseline Baseline { get; }
    public ProcessMetricSnapshot Snapshot { get; }

    // Стектрейс при обнаружении утечки (может быть null)
    public string? StackTrace { get; set; }
}
