using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Options;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Detection;

// Стратегия обнаружения утечек на основе порогов и трендов
public sealed class ThresholdLeakDetectionStrategy
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

    // Основной метод анализа - сравнивает текущие метрики с baseline
    public LeakDetectionInsight Analyze(ProcessMetricSnapshot snapshot, ProcessBaseline baseline)
    {
        // Проверяем достаточность данных
        if (!HasEnoughBaselineData(baseline))
        {
            return CreateNoLeakInsight(snapshot, baseline, "Недостаточно данных baseline");
        }

        // Считаем метрики роста
        var metrics = CalculateMetrics(snapshot, baseline);
        
        // Определяем индикаторы утечки
        var leakIndicators = DetectLeakIndicators(metrics);
        
        // Проверяем GC pressure (частые Gen2 сборки = проблема)
        if (snapshot.GcMetrics != null)
        {
            if (snapshot.GcMetrics.Gen2CollectionsPerSec > 1.0)
            {
                leakIndicators.Add($"Высокая частота Gen2 GC: {snapshot.GcMetrics.Gen2CollectionsPerSec:F2}/сек (признак managed утечки)");
            }
            
            // Проверяем рост GC heap
            if (snapshot.GcMetrics.HeapSizeMb > 100 && metrics.WorkingSetDelta > 0)
            {
                var gcHeapToWorkingSetRatio = snapshot.GcMetrics.HeapSizeMb / snapshot.WorkingSetMb;
                if (gcHeapToWorkingSetRatio > 0.5 && metrics.WorkingSetGrowthPercent > 10)
                {
                    leakIndicators.Add($"Рост GC heap: {snapshot.GcMetrics.HeapSizeMb:F0} MB ({gcHeapToWorkingSetRatio * 100:F1}% Working Set)");
                }
            }
        }
        
        var hasInitialSuspicion = leakIndicators.Count > 0;

        // Регистрируем подозрение в трекере
        _suspicionTracker.RecordSuspicion(snapshot.ProcessId, hasInitialSuspicion);

        // Проверяем подтверждение (N циклов подряд)
        var isLeakConfirmed = _suspicionTracker.IsLeakConfirmed(snapshot.ProcessId);
        
        // Информируем если еще ждем подтверждения
        if (hasInitialSuspicion && !isLeakConfirmed && _options.LeakConfirmationCycles > 1)
        {
            leakIndicators.Add($"(требуется подтверждение в {_options.LeakConfirmationCycles} циклах)");
        }

        // Проверяем тренд (линейная регрессия)
        double? trendValue = null;
        if (_options.EnableTrendAnalysis && baseline.TrendWorkingSetMb.HasValue)
        {
            trendValue = baseline.TrendWorkingSetMb.Value;
            if (trendValue > 3.0)
            {
                leakIndicators.Add($"Тренд роста: +{trendValue:F1} MB/цикл");
            }
        }

        var isLeak = isLeakConfirmed;
        
        // Сильный тренд тоже считаем утечкой
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
        // Используем медиану если есть, иначе среднее
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
        // Комбинированная проверка: процент + абсолютное значение
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
        if (metrics.HandleGrowthPercent >= _options.HandleLeakThresholdPercent)
        {
            return true;
        }
        
        // Доп. проверка для малых baseline
        var handleDelta = (metrics.HandleBaseline * metrics.HandleGrowthPercent / 100.0);
        
        if (metrics.HandleBaseline < 50)
        {
            // Для малых baseline более чувствительные пороги
            if (handleDelta >= 10.0 || metrics.HandleGrowthPercent >= 15.0)
            {
                return true;
            }
        }
        else
        {
            // Для больших baseline строже
            const double absoluteHandleThreshold = 20.0;
            const double smallBaselinePercentThreshold = 20.0;
            
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
        return new LeakDetectionInsight(
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
    }

    // Очистка истории для неактивных процессов
    public void PruneSuspicionHistory(IEnumerable<int> activeProcessIds)
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
