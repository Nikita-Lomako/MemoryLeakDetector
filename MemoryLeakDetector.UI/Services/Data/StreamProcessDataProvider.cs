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
            var insightsByProcess = result.Insights.ToDictionary(i => i.ProcessId);
            var snapshots = new List<ProcessSnapshot>();

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

                    var insight = insightsByProcess.GetValueOrDefault(process.ProcessId);
                    var baseline = insight?.BaselineWorkingSetMb ?? process.WorkingSetMb;

                    snapshots.Add(new ProcessSnapshot(
                        process.ProcessName,
                        process.ProcessId,
                        process.WorkingSetMb,
                        process.VirtualMemoryMb,
                        process.HandleCount,
                        baseline,
                        insight?.IsLeakSuspected ?? false,
                        new ReadOnlyCollection<TrendPoint>(trend.ToList()),
                        process.CpuUsagePercent,
                        process.GcHeapSizeMb,
                        process.Gen2CollectionsPerSec));
                }

                _current = snapshots;
            }

            ProcessesUpdated?.Invoke(this, EventArgs.Empty);
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

