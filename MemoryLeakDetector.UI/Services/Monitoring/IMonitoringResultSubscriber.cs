using MemoryLeakDetector.Core.Contracts;

namespace MemoryLeakDetector.UI.Services.Monitoring
{
    public interface IMonitoringResultSubscriber
    {
        IAsyncEnumerable<MonitoringResultDto> ListenAsync(CancellationToken cancellationToken);
    }
}

