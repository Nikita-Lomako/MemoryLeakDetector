using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MemoryLeakDetector.Core.Services.Metrics;

public sealed class ProcessMetricsCollector : IProcessMetricsCollector
{
    private readonly ILogger<ProcessMetricsCollector> _logger;
    private readonly MonitoringOptions _options;
    private readonly IGcMetricsProvider _gcMetricsProvider;
    private readonly ConcurrentDictionary<int, CpuSample> _cpuSamples = new();

    public ProcessMetricsCollector(
        ILogger<ProcessMetricsCollector> logger,
        IOptions<MonitoringOptions> options,
        IGcMetricsProvider gcMetricsProvider)
    {
        _logger = logger;
        _options = options.Value;
        _gcMetricsProvider = gcMetricsProvider;
    }

    public Task<IReadOnlyCollection<ProcessMetricSnapshot>> CollectAsync(CancellationToken cancellationToken)
    {
        var capturedAt = DateTime.UtcNow;
        var snapshots = new List<ProcessMetricSnapshot>();
        Process[] processes = Array.Empty<Process>();

        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate processes");
            return Task.FromResult<IReadOnlyCollection<ProcessMetricSnapshot>>(snapshots);
        }

        foreach (var process in processes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!process.Responding || process.HasExited)
                {
                    continue;
                }

                if (!_options.IncludeSystemProcesses && IsSystemProcess(process))
                {
                    continue;
                }

                var workingSetMb = BytesToMegabytes(process.WorkingSet64);
                var virtualMemoryMb = BytesToMegabytes(process.VirtualMemorySize64);
                var handles = process.HandleCount;
                var cpuUsage = CalculateCpuUsage(process, capturedAt);
                var gcMetrics = _gcMetricsProvider.TryCollect(process.Id, process.ProcessName);

                snapshots.Add(new ProcessMetricSnapshot(
                    process.Id,
                    process.ProcessName,
                    workingSetMb,
                    virtualMemoryMb,
                    handles,
                    capturedAt,
                    cpuUsage,
                    gcMetrics));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to collect metrics for PID {ProcessId}", process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        var ordered = snapshots
            .OrderByDescending(snapshot => snapshot.WorkingSetMb)
            .Take(_options.MaxProcesses)
            .ToList();

        PruneCpuSamples(ordered);

        return Task.FromResult<IReadOnlyCollection<ProcessMetricSnapshot>>(ordered);
    }

    private void PruneCpuSamples(IReadOnlyCollection<ProcessMetricSnapshot> activeSnapshots)
    {
        var activeIds = activeSnapshots.Select(s => s.ProcessId).ToHashSet();
        foreach (var key in _cpuSamples.Keys)
        {
            if (!activeIds.Contains(key))
            {
                _cpuSamples.TryRemove(key, out _);
            }
        }
    }

    private double? CalculateCpuUsage(Process process, DateTime capturedAtUtc)
    {
        try
        {
            var totalProcessorTime = process.TotalProcessorTime;
            var sample = new CpuSample(totalProcessorTime, capturedAtUtc);

            if (_cpuSamples.TryGetValue(process.Id, out var previous))
            {
                var elapsedMs = (capturedAtUtc - previous.TimestampUtc).TotalMilliseconds;
                if (elapsedMs > 0)
                {
                    var cpuDelta = (totalProcessorTime - previous.TotalProcessorTime).TotalMilliseconds;
                    var usage = cpuDelta / elapsedMs * 100 / Environment.ProcessorCount;
                    _cpuSamples[process.Id] = sample;
                    return Math.Round(Math.Clamp(usage, 0, 100), 2);
                }
            }

            _cpuSamples[process.Id] = sample;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CPU usage calculation failed for PID {ProcessId}", process.Id);
        }

        return null;
    }

    private static double BytesToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 2);

    private static bool IsSystemProcess(Process process)
    {
        return process.SessionId == 0 ||
               string.Equals(process.ProcessName, "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(process.ProcessName, "Idle", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CpuSample(TimeSpan TotalProcessorTime, DateTime TimestampUtc);
}

