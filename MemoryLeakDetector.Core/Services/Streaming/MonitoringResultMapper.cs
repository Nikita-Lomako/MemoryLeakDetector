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
                CapturedAtUtc = snapshot.CapturedAtUtc
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
                BaselineHandleCount = insight.Baseline.AverageHandleCount
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

