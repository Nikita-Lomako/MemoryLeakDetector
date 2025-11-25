using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Monitoring;

public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IProcessMetricsCollector _collector;
    private readonly IBaselineRepository _baselineRepository;
    private readonly ILeakDetectionStrategy _leakDetectionStrategy;
    private readonly ILogger<MonitoringCoordinator> _logger;

    public MonitoringCoordinator(
        IProcessMetricsCollector collector,
        IBaselineRepository baselineRepository,
        ILeakDetectionStrategy leakDetectionStrategy,
        ILogger<MonitoringCoordinator> logger)
    {
        _collector = collector;
        _baselineRepository = baselineRepository;
        _leakDetectionStrategy = leakDetectionStrategy;
        _logger = logger;
    }

    public async Task<MonitoringCycleResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var snapshots = await _collector.CollectAsync(cancellationToken);
        var insights = new List<LeakDetectionInsight>();
        var errors = 0;

        foreach (var snapshot in snapshots)
        {
            try
            {
                var baseline = _baselineRepository.Update(snapshot);
                var insight = _leakDetectionStrategy.Analyze(snapshot, baseline);
                insights.Add(insight);

                if (insight.IsLeakSuspected)
                {
                    _logger.LogWarning("Leak suspected for {ProcessName} ({ProcessId}): {Reason}", snapshot.ProcessName, snapshot.ProcessId, insight.Reason);
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to analyze process {ProcessName} ({ProcessId})", snapshot.ProcessName, snapshot.ProcessId);
            }
        }

        _baselineRepository.PruneInactive(snapshots.Select(snapshot => snapshot.ProcessId));

        var duration = DateTimeOffset.UtcNow - started;
        return new MonitoringCycleResult(started, duration, snapshots, insights, errors);
    }
}

