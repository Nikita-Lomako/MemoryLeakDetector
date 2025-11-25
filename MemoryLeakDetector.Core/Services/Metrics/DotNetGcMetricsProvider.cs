using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using Microsoft.Extensions.Logging;

namespace MemoryLeakDetector.Core.Services.Metrics;

[SupportedOSPlatform("windows")]
public sealed class DotNetGcMetricsProvider : IGcMetricsProvider
{
    private readonly ILogger<DotNetGcMetricsProvider> _logger;
    private readonly ConcurrentDictionary<int, string?> _instanceCache = new();
    private readonly object _categoryLock = new();
    private PerformanceCounterCategory? _category;

    public DotNetGcMetricsProvider(ILogger<DotNetGcMetricsProvider> logger)
    {
        _logger = logger;
    }

    public GcMetrics? TryCollect(int processId, string processName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var instanceName = ResolveInstanceName(processId, processName);
            if (string.IsNullOrEmpty(instanceName))
            {
                return null;
            }

            using var heapCounter = CreateCounter("# Bytes in all Heaps", instanceName);
            using var lohCounter = CreateCounter("Large Object Heap size", instanceName);
            using var gen0Counter = CreateCounter("Gen 0 Collections/sec", instanceName);
            using var gen2Counter = CreateCounter("Gen 2 Collections/sec", instanceName);

            if (heapCounter is null || lohCounter is null || gen0Counter is null || gen2Counter is null)
            {
                return null;
            }

            var heapSizeMb = BytesToMegabytes(heapCounter.NextSample().RawValue);
            var lohMb = BytesToMegabytes(lohCounter.NextSample().RawValue);
            var gen0 = Math.Round(gen0Counter.NextValue(), 2);
            var gen2 = Math.Round(gen2Counter.NextValue(), 2);

            return new GcMetrics(heapSizeMb, lohMb, gen0, gen2);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GC metrics collection failed for PID {Pid}", processId);
            return null;
        }
    }

    private string? ResolveInstanceName(int processId, string processName)
    {
        if (_instanceCache.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        var category = GetCategory();
        if (category is null || !category.CounterExists("Process ID"))
        {
            _instanceCache[processId] = null;
            return null;
        }

        foreach (var instance in category.GetInstanceNames())
        {
            try
            {
                using var counter = new PerformanceCounter(".NET CLR Memory", "Process ID", instance, readOnly: true);
                if ((int)counter.RawValue == processId)
                {
                    _instanceCache[processId] = instance;
                    return instance;
                }
            }
            catch
            {
                // ignored
            }
        }

        _instanceCache[processId] = null;
        return null;
    }

    private PerformanceCounterCategory? GetCategory()
    {
        if (_category is not null)
        {
            return _category;
        }

        lock (_categoryLock)
        {
            _category ??= PerformanceCounterCategory.GetCategories()
                .FirstOrDefault(cat => string.Equals(cat.CategoryName, ".NET CLR Memory", StringComparison.OrdinalIgnoreCase));
            return _category;
        }
    }

    private static PerformanceCounter? CreateCounter(string counterName, string instanceName)
    {
        try
        {
            return new PerformanceCounter(".NET CLR Memory", counterName, instanceName, readOnly: true);
        }
        catch
        {
            return null;
        }
    }

    private static double BytesToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 2);
}

