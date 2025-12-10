using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Core.Services.Detection;

/// <summary>
/// Стратегия детекции утечек на основе пороговых значений.
/// Использует сравнение текущих метрик с baseline (медиана или среднее), проверку превышения порогов,
/// проверку устойчивости утечки и трендовый анализ.
/// </summary>
public sealed class ThresholdLeakDetectionStrategy : ILeakDetectionStrategy
{
    private readonly MonitoringOptions _options;
    private readonly LeakSuspicionTracker _suspicionTracker;

    public ThresholdLeakDetectionStrategy(
        IOptions<MonitoringOptions> options,
        LeakSuspicionTracker suspicionTracker)
    {
        _options = options.Value;
        _suspicionTracker = suspicionTracker;
    }

    public string Name => "threshold";

    public LeakDetectionInsight Analyze(ProcessMetricSnapshot snapshot, ProcessBaseline baseline)
    {
        if (!HasEnoughBaselineData(baseline))
        {
            return CreateNoLeakInsight(snapshot, baseline, "Недостаточно данных baseline");
        }

        var metrics = CalculateMetrics(snapshot, baseline);
        var leakIndicators = DetectLeakIndicators(metrics);
        var hasInitialSuspicion = leakIndicators.Count > 0;

        // Регистрируем подозрение
        _suspicionTracker.RecordSuspicion(snapshot.ProcessId, hasInitialSuspicion);

        // Проверяем устойчивость утечки
        var isLeakConfirmed = _suspicionTracker.IsLeakConfirmed(snapshot.ProcessId);
        
        // Если утечка не подтверждена, но есть подозрение - добавляем информацию
        if (hasInitialSuspicion && !isLeakConfirmed)
        {
            leakIndicators.Add($"(требуется подтверждение в {_options.LeakConfirmationCycles} циклах)");
        }

        // Проверяем тренд для дополнительной информации
        if (_options.EnableTrendAnalysis && baseline.TrendWorkingSetMb.HasValue)
        {
            var trend = baseline.TrendWorkingSetMb.Value;
            if (trend > 5.0) // Рост более 5 MB за цикл
            {
                leakIndicators.Add($"Тренд роста: +{trend:F1} MB/цикл");
            }
        }

        var isLeak = isLeakConfirmed;
        var reason = isLeak || hasInitialSuspicion
            ? string.Join("; ", leakIndicators)
            : "Отклонения в пределах baseline";

        return CreateInsight(snapshot, baseline, isLeak, reason, metrics);
    }

    private bool HasEnoughBaselineData(ProcessBaseline baseline)
    {
        return baseline.SampleCount >= _options.MinSamplesForLeakDetection;
    }

    private static GrowthMetrics CalculateMetrics(ProcessMetricSnapshot snapshot, ProcessBaseline baseline)
    {
        // Используем медиану если доступна, иначе среднее
        var workingSetBaseline = baseline.MedianWorkingSetMb > 0 
            ? baseline.MedianWorkingSetMb 
            : baseline.AverageWorkingSetMb;
            
        var virtualMemoryBaseline = baseline.MedianVirtualMemoryMb > 0 
            ? baseline.MedianVirtualMemoryMb 
            : baseline.AverageVirtualMemoryMb;
            
        var handleBaseline = baseline.MedianHandleCount > 0 
            ? baseline.MedianHandleCount 
            : baseline.AverageHandleCount;

        return new GrowthMetrics
        {
            WorkingSetDelta = snapshot.WorkingSetMb - workingSetBaseline,
            WorkingSetGrowthPercent = CalculatePercentGrowth(workingSetBaseline, snapshot.WorkingSetMb),
            VirtualMemoryGrowthPercent = CalculatePercentGrowth(virtualMemoryBaseline, snapshot.VirtualMemoryMb),
            HandleGrowthPercent = CalculatePercentGrowth(handleBaseline, snapshot.HandleCount)
        };
    }

    private List<string> DetectLeakIndicators(GrowthMetrics metrics)
    {
        var indicators = new List<string>();

        if (IsWorkingSetLeak(metrics))
        {
            indicators.Add($"Working set +{metrics.WorkingSetDelta:F0} MB ({metrics.WorkingSetGrowthPercent:F1}%)");
        }

        if (IsVirtualMemoryLeak(metrics))
        {
            indicators.Add($"Virtual memory +{metrics.VirtualMemoryGrowthPercent:F1}%");
        }

        if (IsHandleLeak(metrics))
        {
            indicators.Add($"Handles +{metrics.HandleGrowthPercent:F1}%");
        }

        return indicators;
    }

    private bool IsWorkingSetLeak(GrowthMetrics metrics)
    {
        return metrics.WorkingSetGrowthPercent >= _options.WorkingSetLeakThresholdPercent &&
               metrics.WorkingSetDelta >= _options.WorkingSetLeakThresholdMb;
    }

    private bool IsVirtualMemoryLeak(GrowthMetrics metrics)
    {
        return metrics.VirtualMemoryGrowthPercent >= _options.VirtualMemoryLeakThresholdPercent;
    }

    private bool IsHandleLeak(GrowthMetrics metrics)
    {
        return metrics.HandleGrowthPercent >= _options.HandleLeakThresholdPercent;
    }

    private static double CalculatePercentGrowth(double baselineValue, double currentValue)
    {
        if (baselineValue <= 0)
        {
            return 0;
        }

        return Math.Round(((currentValue - baselineValue) / baselineValue) * 100, 2);
    }

    private static LeakDetectionInsight CreateNoLeakInsight(
        ProcessMetricSnapshot snapshot,
        ProcessBaseline baseline,
        string reason)
    {
        return CreateInsight(snapshot, baseline, false, reason, new GrowthMetrics());
    }

    private static LeakDetectionInsight CreateInsight(
        ProcessMetricSnapshot snapshot,
        ProcessBaseline baseline,
        bool isLeak,
        string reason,
        GrowthMetrics metrics)
    {
        var insight = new LeakDetectionInsight(
            snapshot.ProcessId,
            snapshot.ProcessName,
            isLeak,
            metrics.WorkingSetGrowthPercent,
            metrics.WorkingSetDelta,
            metrics.VirtualMemoryGrowthPercent,
            metrics.HandleGrowthPercent,
            reason,
            baseline,
            snapshot);

        insight.DetectionStrategy = "threshold";

        return insight;
    }

    /// <summary>
    /// Очищает историю подозрений для неактивных процессов.
    /// Вызывается из MonitoringCoordinator.
    /// </summary>
    internal void PruneSuspicionHistory(IEnumerable<int> activeProcessIds)
    {
        _suspicionTracker.PruneInactive(activeProcessIds);
    }

    private sealed class GrowthMetrics
    {
        public double WorkingSetDelta { get; init; }
        public double WorkingSetGrowthPercent { get; init; }
        public double VirtualMemoryGrowthPercent { get; init; }
        public double HandleGrowthPercent { get; init; }
    }
}
