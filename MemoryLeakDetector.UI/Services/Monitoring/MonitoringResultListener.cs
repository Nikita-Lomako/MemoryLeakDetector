using System.Threading;
using System.Threading.Tasks;
using MemoryLeakDetector.UI.Services.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MemoryLeakDetector.UI.Services.Monitoring
{
    public sealed class MonitoringResultListener : BackgroundService
    {
        private readonly IMonitoringResultSubscriber _subscriber;
        private readonly StreamProcessDataProvider _dataProvider;
        private readonly ILogger<MonitoringResultListener> _logger;

        public MonitoringResultListener(
            IMonitoringResultSubscriber subscriber,
            StreamProcessDataProvider dataProvider,
            ILogger<MonitoringResultListener> logger)
        {
            _subscriber = subscriber;
            _dataProvider = dataProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var result in _subscriber.ListenAsync(stoppingToken))
            {
                _dataProvider.Update(result);
                _logger.LogDebug("Monitoring result received with {ProcessCount} processes", result.Processes.Count);
            }
        }
    }
}

