namespace MemoryLeakDetector.Core.Models;

// Снимок метрик процесса в конкретный момент времени
public sealed class ProcessMetricSnapshot
{
    public ProcessMetricSnapshot(
        int processId,
        string processName,
        double workingSetMb,
        double virtualMemoryMb,
        int handleCount,
        DateTime capturedAtUtc,
        double? cpuUsagePercent = null,
        GcMetrics? gcMetrics = null)
    {
        ProcessId = processId;
        ProcessName = processName;
        WorkingSetMb = workingSetMb;
        VirtualMemoryMb = virtualMemoryMb;
        HandleCount = handleCount;
        CapturedAtUtc = capturedAtUtc;
        CpuUsagePercent = cpuUsagePercent;
        GcMetrics = gcMetrics;
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    
    // Working Set - физическая RAM, используемая процессом (MB)
    public double WorkingSetMb { get; }
    
    // Virtual Memory - виртуальная память процесса (MB)
    public double VirtualMemoryMb { get; }
    
    // Количество открытых handles (файлы, сокеты, таймеры и т.д.)
    public int HandleCount { get; }
    
    public DateTime CapturedAtUtc { get; }
    
    // CPU usage в процентах (может быть null)
    public double? CpuUsagePercent { get; }
    
    // GC метрики для .NET процессов
    public GcMetrics? GcMetrics { get; }
}
