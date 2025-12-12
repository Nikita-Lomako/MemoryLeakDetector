using System.Collections.Concurrent;
using System.Linq;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.Core.Services.History;

// In-memory хранилище истории результатов мониторинга
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

        // Удаляем старые если превысили лимит
        while (_results.Count > _maxItems && _results.TryDequeue(out _))
        {
        }
    }

    public MonitoringResultDto? GetLatest()
    {
        return _results.LastOrDefault();
    }

    public IReadOnlyList<MonitoringResultDto> GetRange(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var result = new List<MonitoringResultDto>();

        foreach (var item in _results)
        {
            if (from is not null && item.StartedUtc < from.Value)
            {
                continue;
            }

            if (to is not null && item.StartedUtc > to.Value)
            {
                continue;
            }

            result.Add(item);
        }

        if (from is not null || to is not null)
        {
            result.Sort((a, b) => a.StartedUtc.CompareTo(b.StartedUtc));
        }

        return result;
    }
}
