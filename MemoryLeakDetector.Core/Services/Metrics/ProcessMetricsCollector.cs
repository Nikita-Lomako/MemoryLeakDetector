using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MemoryLeakDetector.Core.Services.Metrics;

public sealed class ProcessMetricsCollector : IProcessMetricsCollector
{
    private readonly ILogger<ProcessMetricsCollector> _logger;
    private readonly MonitoringOptions _options;

    public ProcessMetricsCollector(
        ILogger<ProcessMetricsCollector> logger,
        IOptions<MonitoringOptions> options)
    {
        _logger = logger;
        _options = options.Value;
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

                snapshots.Add(new ProcessMetricSnapshot(
                    process.Id,
                    process.ProcessName,
                    workingSetMb,
                    virtualMemoryMb,
                    handles,
                    capturedAt));
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

        return Task.FromResult<IReadOnlyCollection<ProcessMetricSnapshot>>(ordered);
    }

    private static double BytesToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 2);

    private static bool IsSystemProcess(Process process)
    {
        return process.SessionId == 0 ||
               string.Equals(process.ProcessName, "System", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(process.ProcessName, "Idle", StringComparison.OrdinalIgnoreCase);
    }
}

