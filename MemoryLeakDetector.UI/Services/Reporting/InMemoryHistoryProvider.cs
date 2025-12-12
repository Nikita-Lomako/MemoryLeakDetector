using System.Collections.Concurrent;
using System.Linq;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.UI.Services.Reporting;

// Провайдер истории мониторинга - накапливает данные из потока результатов
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
        // Оптимизация: избегаем полного копирования через ToArray()
        var result = new List<MonitoringResultDto>();

        foreach (var item in _results)
        {
            if (from is not null && item.StartedUtc < from.Value)
            {
                continue;
            }

            if (to is not null)
            {
                // Включаем результаты до конца выбранного дня (23:59:59)
                var toDate = to.Value.Date.AddDays(1).AddSeconds(-1);
                if (item.StartedUtc > toDate)
                {
                    continue;
                }
            }

            result.Add(item);
        }

        // Сортируем только если нужно
        if (from is not null || to is not null)
        {
            result.Sort((a, b) => a.StartedUtc.CompareTo(b.StartedUtc));
        }

        return result;
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

