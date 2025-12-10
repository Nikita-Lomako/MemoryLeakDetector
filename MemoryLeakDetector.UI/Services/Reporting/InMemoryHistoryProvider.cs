using System.Collections.Concurrent;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.UI.Services.Reporting;

/// <summary>
/// Провайдер истории мониторинга, который накапливает данные из потока результатов.
/// Для полной интеграции в будущем можно расширить для получения истории через Named Pipe из Service.
/// </summary>
public sealed class InMemoryHistoryProvider
{
    private readonly ConcurrentQueue<MonitoringResultDto> _results = new();
    private readonly int _maxItems;

    public InMemoryHistoryProvider(int maxItems = 1000)
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
            // Включаем результаты до конца выбранного дня (23:59:59)
            var toDate = to.Value.Date.AddDays(1).AddSeconds(-1);
            query = query.Where(r => r.StartedUtc <= toDate);
        }

        return query.OrderBy(r => r.StartedUtc).ToList();
    }
    
    public int GetTotalCount()
    {
        return _results.Count;
    }

    public IReadOnlyList<LeakInsightDto> GetLeaks(int recentCycles = 5)
    {
        if (recentCycles <= 0)
        {
            recentCycles = 5;
        }

        var allResults = GetRange();
        if (allResults.Count == 0)
        {
            return Array.Empty<LeakInsightDto>();
        }

        var skip = Math.Max(0, allResults.Count - recentCycles);
        var window = allResults.Skip(skip);

        return window
            .SelectMany(r => r.Insights)
            .Where(i => i.IsLeakSuspected)
            .OrderByDescending(i => i.WorkingSetGrowthPercent)
            .ThenByDescending(i => i.WorkingSetDeltaMb)
            .ToList();
    }
}

