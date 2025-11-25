using System.Linq;

namespace MemoryLeakDetector.Core.Models;

public sealed class MonitoringCycleResult
{
    public MonitoringCycleResult(
        DateTimeOffset startedUtc,
        TimeSpan duration,
        IReadOnlyCollection<ProcessMetricSnapshot> snapshots,
        IReadOnlyCollection<LeakDetectionInsight> insights,
        int errorCount)
    {
        StartedUtc = startedUtc;
        Duration = duration;
        Snapshots = snapshots;
        Insights = insights;
        ErrorCount = errorCount;
    }

    public DateTimeOffset StartedUtc { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyCollection<ProcessMetricSnapshot> Snapshots { get; }
    public IReadOnlyCollection<LeakDetectionInsight> Insights { get; }
    public int ErrorCount { get; }

    public int ActiveProcessCount => Snapshots.Count;
    public int LeakSuspicions => Insights.Count(insight => insight.IsLeakSuspected);
}

