using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Core.Services.Detection;

public sealed class ThresholdLeakDetectionStrategy : ILeakDetectionStrategy
{
    private readonly MonitoringOptions _options;

    public ThresholdLeakDetectionStrategy(IOptions<MonitoringOptions> options)
    {
        _options = options.Value;
    }

    public LeakDetectionInsight Analyze(ProcessMetricSnapshot snapshot, ProcessBaseline baseline)
    {
        if (baseline.SampleCount < _options.MinSamplesForLeakDetection)
        {
            return CreateInsight(snapshot, baseline, false, "Недостаточно данных baseline");
        }

        var workingSetDelta = snapshot.WorkingSetMb - baseline.AverageWorkingSetMb;
        var workingSetGrowthPercent = PercentGrowth(baseline.AverageWorkingSetMb, snapshot.WorkingSetMb);
        var virtualGrowthPercent = PercentGrowth(baseline.AverageVirtualMemoryMb, snapshot.VirtualMemoryMb);
        var handleGrowthPercent = PercentGrowth(baseline.AverageHandleCount, snapshot.HandleCount);

        var reasons = new List<string>();

        if (workingSetGrowthPercent >= _options.WorkingSetLeakThresholdPercent &&
            workingSetDelta >= _options.WorkingSetLeakThresholdMb)
        {
            reasons.Add($"Working set +{workingSetDelta:F0} MB ({workingSetGrowthPercent:F1}%)");
        }

        if (virtualGrowthPercent >= _options.VirtualMemoryLeakThresholdPercent)
        {
            reasons.Add($"Virtual memory +{virtualGrowthPercent:F1}%");
        }

        if (handleGrowthPercent >= _options.HandleLeakThresholdPercent)
        {
            reasons.Add($"Handles +{handleGrowthPercent:F1}%");
        }

        var isLeak = reasons.Count > 0;
        var reasonText = isLeak ? string.Join("; ", reasons) : "Отклонения в пределах baseline";

        return CreateInsight(snapshot, baseline, isLeak, reasonText, workingSetGrowthPercent, workingSetDelta, virtualGrowthPercent, handleGrowthPercent);
    }

    private static double PercentGrowth(double baselineValue, double currentValue)
    {
        if (baselineValue <= 0)
        {
            return 0;
        }

        return Math.Round(((currentValue - baselineValue) / baselineValue) * 100, 2);
    }

    private static LeakDetectionInsight CreateInsight(
        ProcessMetricSnapshot snapshot,
        ProcessBaseline baseline,
        bool isLeak,
        string reason,
        double workingSetGrowthPercent = 0,
        double workingSetDelta = 0,
        double virtualMemoryGrowthPercent = 0,
        double handleGrowthPercent = 0)
    {
        return new LeakDetectionInsight(
            snapshot.ProcessId,
            snapshot.ProcessName,
            isLeak,
            workingSetGrowthPercent,
            workingSetDelta,
            virtualMemoryGrowthPercent,
            handleGrowthPercent,
            reason,
            baseline,
            snapshot);
    }
}

