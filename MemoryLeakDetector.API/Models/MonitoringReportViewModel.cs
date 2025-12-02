using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.API.Models;

public sealed class MonitoringReportViewModel
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public IReadOnlyList<MonitoringResultDto> Results { get; init; } = Array.Empty<MonitoringResultDto>();
}


