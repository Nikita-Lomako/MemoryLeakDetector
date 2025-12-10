using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Services.Detection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Monitoring;

public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IProcessMetricsCollector _collector;
    private readonly IBaselineRepository _baselineRepository;
    private readonly ILeakDetectionStrategy _leakDetectionStrategy;
    private readonly IStackTraceProvider _stackTraceProvider;
    private readonly ILogger<MonitoringCoordinator> _logger;

    public MonitoringCoordinator(
        IProcessMetricsCollector collector,
        IBaselineRepository baselineRepository,
        ILeakDetectionStrategy leakDetectionStrategy,
        IStackTraceProvider stackTraceProvider,
        ILogger<MonitoringCoordinator> logger)
    {
        _collector = collector;
        _baselineRepository = baselineRepository;
        _leakDetectionStrategy = leakDetectionStrategy;
        _stackTraceProvider = stackTraceProvider;
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

                if (insight.IsLeakSuspected)
                {
                    CaptureStackTraceForLeak(insight, snapshot);
                    LogLeakDetection(insight, snapshot);
                }

                insights.Add(insight);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to analyze process {ProcessName} ({ProcessId})", snapshot.ProcessName, snapshot.ProcessId);
            }
        }

        var activeProcessIds = snapshots.Select(snapshot => snapshot.ProcessId).ToList();
        _baselineRepository.PruneInactive(activeProcessIds);
        
        // Очистка истории подозрений для неактивных процессов
        if (_leakDetectionStrategy is ThresholdLeakDetectionStrategy thresholdStrategy)
        {
            thresholdStrategy.PruneSuspicionHistory(activeProcessIds);
        }

        var duration = DateTimeOffset.UtcNow - started;
        return new MonitoringCycleResult(started, duration, snapshots, insights, errors);
    }

    private void CaptureStackTraceForLeak(LeakDetectionInsight insight, ProcessMetricSnapshot snapshot)
    {
        try
        {
            insight.StackTrace = _stackTraceProvider.TryCaptureStackTrace(snapshot.ProcessId, snapshot.ProcessName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to capture stack trace for {ProcessName} ({ProcessId})", snapshot.ProcessName, snapshot.ProcessId);
        }
    }

    private void LogLeakDetection(LeakDetectionInsight insight, ProcessMetricSnapshot snapshot)
    {
        _logger.LogWarning("Leak suspected for {ProcessName} ({ProcessId}): {Reason}", snapshot.ProcessName, snapshot.ProcessId, insight.Reason);
    }
}

