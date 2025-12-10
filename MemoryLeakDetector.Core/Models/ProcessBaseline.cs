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
    
    /// <summary>
    /// Медианное значение Working Set в MB.
    /// Более устойчиво к выбросам чем среднее.
    /// </summary>
    public double MedianWorkingSetMb { get; }
    
    /// <summary>
    /// Медианное значение виртуальной памяти в MB.
    /// </summary>
    public double MedianVirtualMemoryMb { get; }
    
    /// <summary>
    /// Медианное значение количества handles.
    /// </summary>
    public double MedianHandleCount { get; }
    
    /// <summary>
    /// Тренд Working Set (скорость роста в MB/цикл).
    /// Положительное значение означает рост, отрицательное - снижение.
    /// </summary>
    public double? TrendWorkingSetMb { get; }
}

