using System.Collections.Concurrent;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Detection;

// Трекер подозрений на утечки - проверяет устойчивость (N циклов подряд)
public sealed class LeakSuspicionTracker
{
    private readonly ConcurrentDictionary<int, LeakSuspicionHistory> _histories = new();
    private readonly int _confirmationCycles;

    public LeakSuspicionTracker(int confirmationCycles)
    {
        _confirmationCycles = Math.Max(1, confirmationCycles);
    }

    // Записать подозрение для процесса
    public void RecordSuspicion(int processId, bool isSuspected)
    {
        var history = _histories.GetOrAdd(processId, _ => new LeakSuspicionHistory(_confirmationCycles));
        history.Record(isSuspected);
    }

    // Проверить подтверждена ли утечка (подозрение N раз подряд)
    public bool IsLeakConfirmed(int processId)
    {
        if (!_histories.TryGetValue(processId, out var history))
        {
            return false;
        }

        return history.IsConfirmed();
    }

    // Удалить историю для неактивных процессов
    public void PruneInactive(IEnumerable<int> activeProcessIds)
    {
        var activeSet = new HashSet<int>(activeProcessIds);
        
        foreach (var key in _histories.Keys)
        {
            if (!activeSet.Contains(key))
            {
                _histories.TryRemove(key, out _);
            }
        }
    }

    private sealed class LeakSuspicionHistory
    {
        private readonly Queue<bool> _recentSuspicions;
        private readonly int _confirmationCycles;

        public LeakSuspicionHistory(int confirmationCycles)
        {
            _confirmationCycles = confirmationCycles;
            _recentSuspicions = new Queue<bool>(confirmationCycles);
        }

        public void Record(bool isSuspected)
        {
            if (_recentSuspicions.Count >= _confirmationCycles)
            {
                _recentSuspicions.Dequeue();
            }
            _recentSuspicions.Enqueue(isSuspected);
        }

        public bool IsConfirmed()
        {
            // Утечка подтверждена если все N последних циклов подозрительны
            if (_recentSuspicions.Count < _confirmationCycles)
            {
                return false;
            }

            return _recentSuspicions.All(s => s);
        }
    }
}
