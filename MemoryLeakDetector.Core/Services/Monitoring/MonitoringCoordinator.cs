using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Core.Services.Detection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Monitoring;

// Координатор цикла мониторинга - собирает метрики, анализирует, публикует
public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IProcessMetricsCollector _collector;
    private readonly IBaselineRepository _baselineRepository;
    private readonly ThresholdLeakDetectionStrategy _leakDetectionStrategy;
    private readonly IStackTraceProvider _stackTraceProvider;
    private readonly ILogger<MonitoringCoordinator> _logger;

    public MonitoringCoordinator(
        IProcessMetricsCollector collector,
        IBaselineRepository baselineRepository,
        ThresholdLeakDetectionStrategy leakDetectionStrategy,
        IStackTraceProvider stackTraceProvider,
        IOptions<MonitoringOptions> options,
        ILogger<MonitoringCoordinator> logger)
    {
        _collector = collector;
        _baselineRepository = baselineRepository;
        _leakDetectionStrategy = leakDetectionStrategy;
        _stackTraceProvider = stackTraceProvider;
        _logger = logger;
    }

    // Основной цикл мониторинга
    public async Task<MonitoringCycleResult> RunCycleAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var snapshots = await _collector.CollectAsync(cancellationToken);
        var insights = new List<LeakDetectionInsight>();
        var errors = 0;

        foreach (var snapshot in snapshots)
        {
            // Изоляция сбоев - ошибка в одном процессе не влияет на другие
            try
            {
                var baseline = _baselineRepository.Update(snapshot);
                var insight = _leakDetectionStrategy.Analyze(snapshot, baseline);

                if (insight.IsLeakSuspected)
                {
                    // Получаем информацию о процессе (без создания dump)
                    var stackTrace = _stackTraceProvider.TryCaptureStackTrace(snapshot.ProcessId, snapshot.ProcessName);
                    if (stackTrace != null)
                    {
                        insight.StackTrace = stackTrace;
                    }
                    
                    _logger.LogWarning("Leak suspected for {ProcessName} ({ProcessId}): {Reason}", 
                        snapshot.ProcessName, snapshot.ProcessId, insight.Reason);
                }

                insights.Add(insight);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, 
                    "Failed to analyze process {ProcessName} ({ProcessId}). Process will be skipped in this cycle.", 
                    snapshot.ProcessName, snapshot.ProcessId);
            }
        }

        var activeProcessIds = snapshots.Select(snapshot => snapshot.ProcessId).ToList();
        _baselineRepository.PruneInactive(activeProcessIds);
        _leakDetectionStrategy.PruneSuspicionHistory(activeProcessIds);

        var duration = DateTimeOffset.UtcNow - started;
        return new MonitoringCycleResult(started, duration, snapshots, insights, errors);
    }
}
