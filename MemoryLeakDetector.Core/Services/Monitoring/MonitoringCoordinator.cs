using System.Collections.Concurrent;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Core.Services.Detection;
using MemoryLeakDetector.Core.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Monitoring;

public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IProcessMetricsCollector _collector;
    private readonly IBaselineRepository _baselineRepository;
    private readonly ILeakDetectionStrategy _leakDetectionStrategy;
    private readonly IStackTraceProvider _stackTraceProvider;
    private readonly ILogger<MonitoringCoordinator> _logger;
    private readonly MonitoringOptions _options;
    private readonly ConcurrentDictionary<Task, object> _activeDumpTasks = new();

    public MonitoringCoordinator(
        IProcessMetricsCollector collector,
        IBaselineRepository baselineRepository,
        ILeakDetectionStrategy leakDetectionStrategy,
        IStackTraceProvider stackTraceProvider,
        IOptions<MonitoringOptions> options,
        ILogger<MonitoringCoordinator> logger)
    {
        _collector = collector;
        _baselineRepository = baselineRepository;
        _leakDetectionStrategy = leakDetectionStrategy;
        _stackTraceProvider = stackTraceProvider;
        _options = options.Value;
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
            // Изоляция сбоев: ошибка в одном процессе не должна блокировать другие
            try
            {
                var baseline = _baselineRepository.Update(snapshot);
                var insight = _leakDetectionStrategy.Analyze(snapshot, baseline);

                if (insight.IsLeakSuspected)
                {
                    // Асинхронное создание dump не блокирует основной цикл
                    CaptureStackTraceForLeakAsync(insight, snapshot);
                    LogLeakDetection(insight, snapshot);
                }

                insights.Add(insight);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, 
                    "Failed to analyze process {ProcessName} ({ProcessId}). Process will be skipped in this cycle.", 
                    snapshot.ProcessName, snapshot.ProcessId);
                // Продолжаем обработку других процессов - изоляция сбоев
            }
        }

        var activeProcessIds = snapshots.Select(snapshot => snapshot.ProcessId).ToList();
        _baselineRepository.PruneInactive(activeProcessIds);
        
        // Очистка истории подозрений для неактивных процессов
        if (_leakDetectionStrategy is ThresholdLeakDetectionStrategy thresholdStrategy)
        {
            thresholdStrategy.PruneSuspicionHistory(activeProcessIds);
        }

        // Очистка истории rate limiting для неактивных процессов
        if (_stackTraceProvider is RateLimitedStackTraceProvider rateLimitedProvider)
        {
            rateLimitedProvider.PruneInactive(activeProcessIds);
        }

        // Очистка завершенных dump задач
        CleanupCompletedDumpTasks();

        var duration = DateTimeOffset.UtcNow - started;
        return new MonitoringCycleResult(started, duration, snapshots, insights, errors);
    }

    private void CaptureStackTraceForLeakAsync(LeakDetectionInsight insight, ProcessMetricSnapshot snapshot)
    {
        // Если dump файлы отключены (интервал = -1), пропускаем
        if (_options.DumpCreationMinIntervalSeconds < 0)
        {
            return;
        }

        if (_options.CreateDumpsAsync)
        {
            // Асинхронное создание dump - не блокирует основной поток
            var dumpTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Yield(); // Позволяем основному потоку продолжить работу
                    var stackTrace = _stackTraceProvider.TryCaptureStackTrace(snapshot.ProcessId, snapshot.ProcessName);
                    if (stackTrace != null)
                    {
                        insight.StackTrace = stackTrace;
                    }
                }
                catch (Exception ex)
                {
                    // Изоляция ошибок: ошибка создания dump не должна влиять на мониторинг
                    _logger.LogWarning(ex, 
                        "Failed to capture stack trace asynchronously for {ProcessName} ({ProcessId})", 
                        snapshot.ProcessName, snapshot.ProcessId);
                }
            }, CancellationToken.None);

            _activeDumpTasks.TryAdd(dumpTask, null!);
        }
        else
        {
            // Синхронное создание (для обратной совместимости, но с таймаутом)
            CaptureStackTraceForLeak(insight, snapshot);
        }
    }

    private void CaptureStackTraceForLeak(LeakDetectionInsight insight, ProcessMetricSnapshot snapshot)
    {
        try
        {
            // Используем таймаут для предотвращения блокировки
            var timeout = TimeSpan.FromSeconds(10);
            var captureTask = Task.Run(() => 
                _stackTraceProvider.TryCaptureStackTrace(snapshot.ProcessId, snapshot.ProcessName));
            
            if (captureTask.Wait(timeout))
            {
                insight.StackTrace = captureTask.Result;
            }
            else
            {
                _logger.LogWarning(
                    "Stack trace capture timeout for {ProcessName} ({ProcessId}) after {Timeout}s", 
                    snapshot.ProcessName, snapshot.ProcessId, timeout.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            // Изоляция ошибок: ошибка создания dump не должна влиять на мониторинг
            _logger.LogDebug(ex, 
                "Failed to capture stack trace for {ProcessName} ({ProcessId})", 
                snapshot.ProcessName, snapshot.ProcessId);
        }
    }

    private void CleanupCompletedDumpTasks()
    {
        var completedTasks = _activeDumpTasks.Keys.Where(t => t.IsCompleted).ToList();
        foreach (var task in completedTasks)
        {
            _activeDumpTasks.TryRemove(task, out _);
            
            // Проверяем ошибки в завершенных задачах
            if (task.IsFaulted && task.Exception != null)
            {
                _logger.LogWarning(task.Exception, "Async dump task completed with error");
            }
        }
    }

    private void LogLeakDetection(LeakDetectionInsight insight, ProcessMetricSnapshot snapshot)
    {
        _logger.LogWarning("Leak suspected for {ProcessName} ({ProcessId}): {Reason}", 
            snapshot.ProcessName, snapshot.ProcessId, insight.Reason);
    }
}
