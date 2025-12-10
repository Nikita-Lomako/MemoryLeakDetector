using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Abstractions;

/// <summary>
/// Интерфейс для хранения истории результатов мониторинга.
/// </summary>
public interface IMonitoringHistoryStore
{
    /// <summary>
    /// Добавляет результат мониторинга в хранилище.
    /// </summary>
    void Add(MonitoringResultDto result);

    /// <summary>
    /// Получает последний результат мониторинга.
    /// </summary>
    MonitoringResultDto? GetLatest();

    /// <summary>
    /// Получает диапазон результатов мониторинга за указанный период.
    /// </summary>
    /// <param name="from">Начальная дата (включительно). Если null, без ограничения.</param>
    /// <param name="to">Конечная дата (включительно). Если null, без ограничения.</param>
    /// <returns>Отсортированный список результатов мониторинга.</returns>
    IReadOnlyList<MonitoringResultDto> GetRange(DateTimeOffset? from = null, DateTimeOffset? to = null);
}

