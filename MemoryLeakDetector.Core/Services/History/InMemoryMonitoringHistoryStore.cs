using System.Collections.Concurrent;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Services.History;

/// <summary>
/// In-memory реализация хранилища истории результатов мониторинга.
/// </summary>
public sealed class InMemoryMonitoringHistoryStore : IMonitoringHistoryStore
{
    private readonly ConcurrentQueue<MonitoringResultDto> _results = new();
    private readonly int _maxItems;

    public InMemoryMonitoringHistoryStore(int maxItems = 1000)
    {
        _maxItems = maxItems;
    }

    public void Add(MonitoringResultDto result)
    {
        _results.Enqueue(result);

        while (_results.Count > _maxItems && _results.TryDequeue(out _))
        {
            // discard old items
        }
    }

    public MonitoringResultDto? GetLatest()
    {
        return _results.LastOrDefault();
    }

    public IReadOnlyList<MonitoringResultDto> GetRange(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        IEnumerable<MonitoringResultDto> query = _results.ToArray();

        if (from is not null)
        {
            query = query.Where(r => r.StartedUtc >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(r => r.StartedUtc <= to.Value);
        }

        return query.OrderBy(r => r.StartedUtc).ToList();
    }
}

