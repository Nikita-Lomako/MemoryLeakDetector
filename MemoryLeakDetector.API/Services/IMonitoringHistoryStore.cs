using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.API.Services;

public interface IMonitoringHistoryStore
{
    void Add(MonitoringResultDto result);

    MonitoringResultDto? GetLatest();

    IReadOnlyList<MonitoringResultDto> GetRange(DateTimeOffset? from = null, DateTimeOffset? to = null);
}


