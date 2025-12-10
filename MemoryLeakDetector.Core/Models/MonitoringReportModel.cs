using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Models;

/// <summary>
/// Модель данных для отчета по мониторингу памяти.
/// </summary>
public sealed class MonitoringReportModel
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public IReadOnlyList<MonitoringResultDto> Results { get; init; } = Array.Empty<MonitoringResultDto>();
}

