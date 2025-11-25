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
        return tracker.Update(snapshot);
    }

    public ProcessBaseline? Get(int processId)
    {
        return _trackers.TryGetValue(processId, out var tracker)
            ? tracker.ToBaseline()
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

        public ProcessBaseline Update(ProcessMetricSnapshot snapshot)
        {
            lock (_sync)
            {
                EnqueueSample(_workingSetSamples, snapshot.WorkingSetMb);
                EnqueueSample(_virtualMemorySamples, snapshot.VirtualMemoryMb);
                EnqueueSample(_handleSamples, snapshot.HandleCount);
                LastUpdatedUtc = snapshot.CapturedAtUtc;

                return ToBaselineInternal();
            }
        }

        public ProcessBaseline ToBaseline()
        {
            lock (_sync)
            {
                return ToBaselineInternal();
            }
        }

        private ProcessBaseline ToBaselineInternal()
        {
            var sampleCount = _workingSetSamples.Count;

            return new ProcessBaseline(
                ProcessId,
                ProcessName,
                Average(_workingSetSamples),
                Average(_virtualMemorySamples),
                Average(_handleSamples),
                sampleCount,
                LastUpdatedUtc);
        }

        private void EnqueueSample(Queue<double> queue, double value)
        {
            if (queue.Count == _maxSamples)
            {
                queue.Dequeue();
            }
            queue.Enqueue(value);
        }

        private static double Average(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count == 0)
            {
                return 0;
            }

            return Math.Round(list.Average(), 2);
        }
    }
}

