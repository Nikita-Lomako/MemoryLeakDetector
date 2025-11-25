namespace MemoryLeakDetector.Core.Models;

public sealed class GcMetrics
{
    public GcMetrics(double heapSizeMb, double largeObjectHeapMb, double gen0CollectionsPerSec, double gen2CollectionsPerSec)
    {
        HeapSizeMb = heapSizeMb;
        LargeObjectHeapMb = largeObjectHeapMb;
        Gen0CollectionsPerSec = gen0CollectionsPerSec;
        Gen2CollectionsPerSec = gen2CollectionsPerSec;
    }

    public double HeapSizeMb { get; }
    public double LargeObjectHeapMb { get; }
    public double Gen0CollectionsPerSec { get; }
    public double Gen2CollectionsPerSec { get; }
}

