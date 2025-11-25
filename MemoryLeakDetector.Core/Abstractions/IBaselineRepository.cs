using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

public interface IBaselineRepository
{
    ProcessBaseline Update(ProcessMetricSnapshot snapshot);
    ProcessBaseline? Get(int processId);
    void Remove(int processId);
    void PruneInactive(IEnumerable<int> activeProcessIds);
}

