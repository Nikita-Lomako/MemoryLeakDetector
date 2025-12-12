namespace MemoryLeakDetector.Core.Models;

// Baseline метрик процесса - "нормальное" состояние на основе истории
public sealed class ProcessBaseline
{
    public ProcessBaseline(
        int processId,
        string processName,
        double averageWorkingSetMb,
        double averageVirtualMemoryMb,
        double averageHandleCount,
        int sampleCount,
        DateTime lastUpdatedUtc,
        double medianWorkingSetMb = 0,
        double medianVirtualMemoryMb = 0,
        double medianHandleCount = 0,
        double? trendWorkingSetMb = null)
    {
        ProcessId = processId;
        ProcessName = processName;
        AverageWorkingSetMb = averageWorkingSetMb;
        AverageVirtualMemoryMb = averageVirtualMemoryMb;
        AverageHandleCount = averageHandleCount;
        SampleCount = sampleCount;
        LastUpdatedUtc = lastUpdatedUtc;
        MedianWorkingSetMb = medianWorkingSetMb;
        MedianVirtualMemoryMb = medianVirtualMemoryMb;
        MedianHandleCount = medianHandleCount;
        TrendWorkingSetMb = trendWorkingSetMb;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public double AverageWorkingSetMb { get; }
    public double AverageVirtualMemoryMb { get; }
    public double AverageHandleCount { get; }
    public int SampleCount { get; }
    public DateTime LastUpdatedUtc { get; }
    
    // Медиана - более устойчива к выбросам чем среднее
    public double MedianWorkingSetMb { get; }
    public double MedianVirtualMemoryMb { get; }
    public double MedianHandleCount { get; }
    
    // Тренд - скорость роста памяти в MB/цикл (положительный = рост)
    public double? TrendWorkingSetMb { get; }
}
