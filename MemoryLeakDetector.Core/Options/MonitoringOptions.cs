namespace MemoryLeakDetector.Core.Options;

public sealed class MonitoringOptions
{
    public int MaxProcesses { get; set; } = 100;
    public int PollingIntervalMilliseconds { get; set; } = 2000;
    public int BaselineWindow { get; set; } = 120;
    public double WorkingSetLeakThresholdPercent { get; set; } = 25.0;
    public double WorkingSetLeakThresholdMb { get; set; } = 150.0;
    public double VirtualMemoryLeakThresholdPercent { get; set; } = 30.0;
    public double HandleLeakThresholdPercent { get; set; } = 40.0;
    public int MinSamplesForLeakDetection { get; set; } = 5;
    public bool IncludeSystemProcesses { get; set; } = false;
}

