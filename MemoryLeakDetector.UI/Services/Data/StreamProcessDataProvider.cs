using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.UI.Models;

namespace MemoryLeakDetector.UI.Services.Data
{
    public sealed class StreamProcessDataProvider : IProcessDataProvider
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, Queue<TrendPoint>> _trendHistory = new();
        private IReadOnlyCollection<ProcessSnapshot> _current = Array.Empty<ProcessSnapshot>();
        private const int TrendWindow = 60;

        public event EventHandler? ProcessesUpdated;

        public IReadOnlyCollection<ProcessSnapshot> GetProcesses()
        {
            lock (_sync)
            {
                return _current;
            }
        }

        public void Update(MonitoringResultDto result)
        {
            try
            {
                // Оптимизация: создаем словарь вне lock для уменьшения времени блокировки
                var insightsByProcess = result.Insights.ToDictionary(i => i.ProcessId);
                var snapshots = new List<ProcessSnapshot>(result.Processes.Count); // Предварительно выделяем память
                var activeProcessIds = new HashSet<int>(result.Processes.Count);

                // Минимизируем время блокировки - выполняем только критичные операции в lock
                List<int> inactiveKeys;
                lock (_sync)
                {
                    foreach (var process in result.Processes)
                    {
                        var trend = GetOrCreateTrend(process.ProcessId, process.ProcessName);

                        trend.Enqueue(new TrendPoint(
                            process.CapturedAtUtc.ToLocalTime(),
                            process.WorkingSetMb,
                            process.VirtualMemoryMb,
                            process.HandleCount));

                        while (trend.Count > TrendWindow)
                        {
                            trend.Dequeue();
                        }

                        activeProcessIds.Add(process.ProcessId);
                    }

                    // Очистка истории для неактивных процессов
                    inactiveKeys = _trendHistory.Keys.Where(key => !activeProcessIds.Contains(key)).ToList();
                    foreach (var key in inactiveKeys)
                    {
                        _trendHistory.Remove(key);
                    }
                }

                // Создаем snapshots вне lock для уменьшения времени блокировки
                const int maxTrendPoints = 30;
                var trendSnapshots = new Dictionary<int, TrendPoint[]>();
                
                // Копируем все тренды одним lock
                lock (_sync)
                {
                    foreach (var process in result.Processes)
                    {
                        if (!_trendHistory.TryGetValue(process.ProcessId, out var trend))
                        {
                            continue; // Процесс был удален между проверками
                        }

                        // Быстро копируем только нужные данные
                        TrendPoint[] trendArray;
                        if (trend.Count > maxTrendPoints)
                        {
                            trendArray = new TrendPoint[maxTrendPoints];
                            var sourceArray = trend.ToArray();
                            Array.Copy(sourceArray, sourceArray.Length - maxTrendPoints, trendArray, 0, maxTrendPoints);
                        }
                        else
                        {
                            trendArray = trend.ToArray();
                        }
                        
                        trendSnapshots[process.ProcessId] = trendArray;
                    }
                }

                // Создаем snapshots без lock
                foreach (var process in result.Processes)
                {
                    if (!trendSnapshots.TryGetValue(process.ProcessId, out var trendArray))
                    {
                        continue;
                    }

                    var insight = insightsByProcess.GetValueOrDefault(process.ProcessId);
                    var baseline = insight?.BaselineWorkingSetMb ?? process.WorkingSetMb;

                    // Ограничиваем размер StackTrace для экономии памяти (максимум 500 символов)
                    var stackTrace = insight?.StackTrace;
                    if (!string.IsNullOrEmpty(stackTrace) && stackTrace.Length > 500)
                    {
                        stackTrace = stackTrace.Substring(0, 497) + "...";
                    }
                    
                    snapshots.Add(new ProcessSnapshot(
                        process.ProcessName,
                        process.ProcessId,
                        process.WorkingSetMb,
                        process.VirtualMemoryMb,
                        process.HandleCount,
                        baseline,
                        insight?.IsLeakSuspected ?? false,
                        new ReadOnlyCollection<TrendPoint>(trendArray),
                        process.CpuUsagePercent,
                        process.GcHeapSizeMb,
                        process.Gen2CollectionsPerSec,
                        insight?.Reason,
                        stackTrace));
                }

                // Финальное обновление в lock
                lock (_sync)
                {
                    _current = snapshots;
                }

                // Защищаем вызов события от исключений в обработчиках
                try
                {
                    ProcessesUpdated?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in ProcessesUpdated handlers: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in StreamProcessDataProvider.Update: {ex.Message}");
                // Не пробрасываем исключение, чтобы не остановить мониторинг
            }
        }

        private Queue<TrendPoint> GetOrCreateTrend(int processId, string processName)
        {
            if (_trendHistory.TryGetValue(processId, out var queue))
            {
                return queue;
            }

            queue = new Queue<TrendPoint>();
            _trendHistory[processId] = queue;
            return queue;
        }
    }
}

