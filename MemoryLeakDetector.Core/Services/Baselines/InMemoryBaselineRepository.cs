using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System;

namespace MemoryLeakDetector.Core.Services.Baselines;

public sealed class InMemoryBaselineRepository : IBaselineRepository
{
    private readonly ConcurrentDictionary<int, BaselineTracker> _trackers = new();
    private readonly MonitoringOptions _options;

    public InMemoryBaselineRepository(IOptions<MonitoringOptions> options)
    {
        _options = options.Value;
    }

    public ProcessBaseline Update(ProcessMetricSnapshot snapshot)
    {
        var tracker = _trackers.GetOrAdd(snapshot.ProcessId, _ => new BaselineTracker(snapshot.ProcessId, snapshot.ProcessName, _options.BaselineWindow));
        return tracker.Update(snapshot, _options.UseMedianForBaseline, _options.EnableTrendAnalysis);
    }

    public ProcessBaseline? Get(int processId)
    {
        return _trackers.TryGetValue(processId, out var tracker)
            ? tracker.ToBaseline(_options.UseMedianForBaseline, _options.EnableTrendAnalysis)
            : null;
    }

    public void Remove(int processId)
    {
        _trackers.TryRemove(processId, out _);
    }

    public void PruneInactive(IEnumerable<int> activeProcessIds)
    {
        var activeSet = new HashSet<int>(activeProcessIds);

        foreach (var key in _trackers.Keys)
        {
            if (!activeSet.Contains(key))
            {
                _trackers.TryRemove(key, out _);
            }
        }
    }

    private sealed class BaselineTracker
    {
        private readonly object _sync = new();
        private readonly Queue<double> _workingSetSamples;
        private readonly Queue<double> _virtualMemorySamples;
        private readonly Queue<double> _handleSamples;
        private readonly int _maxSamples;

        public BaselineTracker(int processId, string processName, int maxSamples)
        {
            ProcessId = processId;
            ProcessName = processName;
            _maxSamples = Math.Max(1, maxSamples);
            _workingSetSamples = new Queue<double>(_maxSamples);
            _virtualMemorySamples = new Queue<double>(_maxSamples);
            _handleSamples = new Queue<double>(_maxSamples);
        }

        public int ProcessId { get; }
        public string ProcessName { get; }
        public DateTime LastUpdatedUtc { get; private set; }

        public ProcessBaseline Update(ProcessMetricSnapshot snapshot, bool useMedian, bool enableTrendAnalysis)
        {
            lock (_sync)
            {
                EnqueueSample(_workingSetSamples, snapshot.WorkingSetMb);
                EnqueueSample(_virtualMemorySamples, snapshot.VirtualMemoryMb);
                EnqueueSample(_handleSamples, snapshot.HandleCount);
                LastUpdatedUtc = snapshot.CapturedAtUtc;

                return ToBaselineInternal(useMedian, enableTrendAnalysis);
            }
        }

        public ProcessBaseline ToBaseline(bool useMedian, bool enableTrendAnalysis)
        {
            lock (_sync)
            {
                return ToBaselineInternal(useMedian, enableTrendAnalysis);
            }
        }

        private ProcessBaseline ToBaselineInternal(bool useMedian, bool enableTrendAnalysis)
        {
            var sampleCount = _workingSetSamples.Count;
            var workingSetList = _workingSetSamples.ToList();
            var virtualMemoryList = _virtualMemorySamples.ToList();
            var handleList = _handleSamples.ToList();

            var workingSetValue = useMedian 
                ? CalculateMedian(workingSetList) 
                : CalculateAverage(workingSetList);
                
            var virtualMemoryValue = useMedian 
                ? CalculateMedian(virtualMemoryList) 
                : CalculateAverage(virtualMemoryList);
                
            var handleValue = useMedian 
                ? CalculateMedian(handleList) 
                : CalculateAverage(handleList);

            double? trend = null;
            if (enableTrendAnalysis && workingSetList.Count >= 3)
            {
                trend = CalculateTrend(workingSetList);
            }

            return new ProcessBaseline(
                ProcessId,
                ProcessName,
                CalculateAverage(workingSetList),
                CalculateAverage(virtualMemoryList),
                CalculateAverage(handleList),
                sampleCount,
                LastUpdatedUtc,
                useMedian ? workingSetValue : CalculateMedian(workingSetList),
                useMedian ? virtualMemoryValue : CalculateMedian(virtualMemoryList),
                useMedian ? handleValue : CalculateMedian(handleList),
                trend);
        }

        private void EnqueueSample(Queue<double> queue, double value)
        {
            if (queue.Count == _maxSamples)
            {
                queue.Dequeue();
            }
            queue.Enqueue(value);
        }

        private static double CalculateAverage(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            return Math.Round(list.Average(), 2);
        }

        private static double CalculateMedian(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            var sorted = values.OrderBy(x => x).ToList();
            var mid = sorted.Count / 2;

            if (sorted.Count % 2 == 0)
            {
                return Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0, 2);
            }
            else
            {
                return Math.Round(sorted[mid], 2);
            }
        }

        private static double? CalculateTrend(List<double> values)
        {
            if (values.Count < 3)
            {
                return null;
            }

            // Используем линейную регрессию для вычисления тренда
            // Возвращаем наклон линии (slope)
            var n = values.Count;
            var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var y = values.ToArray();

            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumX2 = x.Sum(xi => xi * xi);

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 0.0001)
            {
                return null;
            }

            var slope = (n * sumXY - sumX * sumY) / denominator;
            return Math.Round(slope, 4);
        }
    }
}
