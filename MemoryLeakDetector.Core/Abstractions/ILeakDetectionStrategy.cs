using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface ILeakDetectionStrategy
{
    /// <summary>
    /// Уникальное имя / тип стратегии (например, "threshold", "ml-anomaly").
    /// Нужно для отображения и логирования.
    /// </summary>
    string Name { get; }

    LeakDetectionInsight Analyze(ProcessMetricSnapshot snapshot, ProcessBaseline baseline);
}

