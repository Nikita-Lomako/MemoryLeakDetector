using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Options;
using System.Linq;

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
        
        // Анализ GC pressure (подход PerfView): высокая частота Gen2 сборок указывает на утечку
        // PerfView использует Gen2 Collections/sec > 1 как индикатор утечки managed памяти
        if (snapshot.GcMetrics != null)
        {
            // Если Gen2 сборки происходят чаще чем 1 раз в секунду - это признак утечки
            // Это означает, что GC не успевает освобождать память
            if (snapshot.GcMetrics.Gen2CollectionsPerSec > 1.0)
            {
                leakIndicators.Add($"Высокая частота Gen2 GC: {snapshot.GcMetrics.Gen2CollectionsPerSec:F2}/сек (признак managed утечки)");
            }
            
            // Также проверяем рост GC heap size относительно Working Set
            // Если GC heap растет быстрее чем Working Set, это может указывать на утечку
            if (snapshot.GcMetrics.HeapSizeMb > 100 && metrics.WorkingSetDelta > 0)
            {
                var gcHeapToWorkingSetRatio = snapshot.GcMetrics.HeapSizeMb / snapshot.WorkingSetMb;
                // Если GC heap составляет более 50% Working Set и растет - возможна утечка
                if (gcHeapToWorkingSetRatio > 0.5 && metrics.WorkingSetGrowthPercent > 10)
                {
                    leakIndicators.Add($"Рост GC heap: {snapshot.GcMetrics.HeapSizeMb:F0} MB ({gcHeapToWorkingSetRatio * 100:F1}% Working Set)");
                }
            }
        }
        
        var hasInitialSuspicion = leakIndicators.Count > 0;

        // Регистрируем подозрение
        _suspicionTracker.RecordSuspicion(snapshot.ProcessId, hasInitialSuspicion);

        // Проверяем устойчивость утечки
        // Если LeakConfirmationCycles = 1, утечка обнаруживается сразу
        var isLeakConfirmed = _suspicionTracker.IsLeakConfirmed(snapshot.ProcessId);
        
        // Если утечка не подтверждена, но есть подозрение - добавляем информацию
        // (это может быть только если LeakConfirmationCycles > 1)
        if (hasInitialSuspicion && !isLeakConfirmed && _options.LeakConfirmationCycles > 1)
        {
            leakIndicators.Add($"(требуется подтверждение в {_options.LeakConfirmationCycles} циклах)");
        }

        // Проверяем тренд для дополнительной информации (подход PerfView: анализ трендов)
        // PerfView использует линейную регрессию для выявления устойчивых трендов роста
        double? trendValue = null;
        if (_options.EnableTrendAnalysis && baseline.TrendWorkingSetMb.HasValue)
        {
            trendValue = baseline.TrendWorkingSetMb.Value;
            // Показываем тренд только если он значительный (> 3 MB/цикл)
            // Это уменьшает информационный шум в отчетах
            if (trendValue > 3.0)
            {
                leakIndicators.Add($"Тренд роста: +{trendValue:F1} MB/цикл");
            }
        }

        // При LeakConfirmationCycles = 1 утечка обнаруживается сразу при первом подозрении
        // При LeakConfirmationCycles > 1 требуется подтверждение
        var isLeak = isLeakConfirmed;
        
        // Также считаем утечкой стабильный тренд роста (для обнаружения постепенных утечек)
        // Используем более строгий порог для тренда, чтобы уменьшить false positives
        // Тренд > 10 MB/цикл в течение нескольких циклов - явный признак утечки
        if (!isLeak && trendValue.HasValue && trendValue.Value > 10.0)
        {
            isLeak = true;
            if (!leakIndicators.Any(i => i.Contains("сильный тренд")))
            {
                leakIndicators.Add($"Обнаружен сильный тренд роста: +{trendValue:F1} MB/цикл");
            }
        }
        
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
            HandleGrowthPercent = CalculatePercentGrowth(handleBaseline, snapshot.HandleCount),
            HandleBaseline = handleBaseline
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
        // Комбинированная логика для улучшенного обнаружения утечек:
        // 1. Значительный процентный рост (большие процессы)
        // 2. ИЛИ значительный абсолютный рост (малые процессы)
        // Это баланс между ранним обнаружением и снижением false positives
        return (metrics.WorkingSetGrowthPercent >= _options.WorkingSetLeakThresholdPercent &&
                metrics.WorkingSetDelta >= _options.WorkingSetLeakThresholdMb * 0.5) ||
               metrics.WorkingSetDelta >= _options.WorkingSetLeakThresholdMb;
    }

    private bool IsVirtualMemoryLeak(GrowthMetrics metrics)
    {
        return metrics.VirtualMemoryGrowthPercent >= _options.VirtualMemoryLeakThresholdPercent;
    }

    private bool IsHandleLeak(GrowthMetrics metrics)
    {
        // Для handles используем комбинированный подход:
        // 1. Процентный рост (если baseline достаточно большой)
        // 2. Абсолютное значение для малых baseline (например, рост более 30 handles)
        // Это позволяет обнаруживать утечки таймеров даже при низком baseline
        
        if (metrics.HandleGrowthPercent >= _options.HandleLeakThresholdPercent)
        {
            return true;
        }
        
        // Дополнительная проверка: абсолютный рост handles для раннего обнаружения
        // Вычисляем абсолютный рост handles
        var handleDelta = (metrics.HandleBaseline * metrics.HandleGrowthPercent / 100.0);
        
        // Для малых baseline используем более чувствительные пороги
        // Это критично для обнаружения утечек таймеров, которые могут иметь маленький baseline
        if (metrics.HandleBaseline < 50)
        {
            // Для малых baseline (меньше 50 handles):
            // Рост более 10 handles ИЛИ рост более 15% - утечка
            // Это позволяет обнаруживать утечки таймеров сразу
            if (handleDelta >= 10.0 || metrics.HandleGrowthPercent >= 15.0)
            {
                return true;
            }
        }
        else
        {
            // Для больших baseline (50+ handles):
            // Более строгие пороги для снижения false positives
            const double absoluteHandleThreshold = 20.0; // Минимум 20 handles роста
            const double smallBaselinePercentThreshold = 20.0; // Минимум 20% роста
            
            if (handleDelta >= absoluteHandleThreshold && metrics.HandleGrowthPercent >= smallBaselinePercentThreshold)
            {
                return true;
            }
        }
        
        return false;
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
        return CreateInsight(snapshot, baseline, false, reason, new GrowthMetrics 
        { 
            HandleBaseline = 0 
        });
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
        public double HandleBaseline { get; init; }
    }
}
