using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface ILeakDetectionStrategy
{
    LeakDetectionInsight Analyze(ProcessMetricSnapshot snapshot, ProcessBaseline baseline);
}

