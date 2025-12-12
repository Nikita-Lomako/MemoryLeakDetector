using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Abstractions;

// Хранилище истории результатов мониторинга
public interface IMonitoringHistoryStore
{
    // Добавить результат в хранилище
    void Add(MonitoringResultDto result);

    // Получить последний результат
    MonitoringResultDto? GetLatest();

    // Получить результаты за период (from/to могут быть null)
    IReadOnlyList<MonitoringResultDto> GetRange(DateTimeOffset? from = null, DateTimeOffset? to = null);
}
