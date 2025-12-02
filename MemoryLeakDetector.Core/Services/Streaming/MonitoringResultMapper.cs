using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Contracts;
using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Services.Streaming;

public sealed class MonitoringResultMapper : IMonitoringResultMapper
{
    public MonitoringResultDto Map(MonitoringCycleResult result)
    {
        var processes = result.Snapshots
            .Select(snapshot => new ProcessMetricDto
            {
                ProcessId = snapshot.ProcessId,
                ProcessName = snapshot.ProcessName,
                WorkingSetMb = snapshot.WorkingSetMb,
                VirtualMemoryMb = snapshot.VirtualMemoryMb,
                HandleCount = snapshot.HandleCount,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                CpuUsagePercent = snapshot.CpuUsagePercent,
                GcHeapSizeMb = snapshot.GcMetrics?.HeapSizeMb,
                LargeObjectHeapMb = snapshot.GcMetrics?.LargeObjectHeapMb,
                Gen0CollectionsPerSec = snapshot.GcMetrics?.Gen0CollectionsPerSec,
                Gen2CollectionsPerSec = snapshot.GcMetrics?.Gen2CollectionsPerSec
            })
            .ToList();

        var insights = result.Insights
            .Select(insight => new LeakInsightDto
            {
                ProcessId = insight.ProcessId,
                ProcessName = insight.ProcessName,
                IsLeakSuspected = insight.IsLeakSuspected,
                WorkingSetGrowthPercent = insight.WorkingSetGrowthPercent,
                WorkingSetDeltaMb = insight.WorkingSetDeltaMb,
                VirtualMemoryGrowthPercent = insight.VirtualMemoryGrowthPercent,
                HandleGrowthPercent = insight.HandleGrowthPercent,
                Reason = insight.Reason,
                BaselineUpdatedAtUtc = insight.Baseline.LastUpdatedUtc,
                BaselineWorkingSetMb = insight.Baseline.AverageWorkingSetMb,
                BaselineVirtualMemoryMb = insight.Baseline.AverageVirtualMemoryMb,
                BaselineHandleCount = insight.Baseline.AverageHandleCount,
                StackTrace = insight.StackTrace,
                DetectionStrategy = insight.DetectionStrategy,
                AnomalyScore = insight.AnomalyScore
            })
            .ToList();

        return new MonitoringResultDto
        {
            StartedUtc = result.StartedUtc,
            Duration = result.Duration,
            Processes = processes,
            Insights = insights,
            ErrorCount = result.ErrorCount
        };
    }
}

