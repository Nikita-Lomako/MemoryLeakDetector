namespace MemoryLeakDetector.Core.Contracts;

public sealed class MonitoringResultDto
{
    public DateTimeOffset StartedUtc { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ProcessMetricDto> Processes { get; init; } = Array.Empty<ProcessMetricDto>();
    public IReadOnlyList<LeakInsightDto> Insights { get; init; } = Array.Empty<LeakInsightDto>();
    public int ErrorCount { get; init; }
}

