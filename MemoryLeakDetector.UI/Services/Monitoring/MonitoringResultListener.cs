using System.Threading;
using System.Threading.Tasks;
using MemoryLeakDetector.UI.Services.Data;
using MemoryLeakDetector.UI.Services.Reporting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MemoryLeakDetector.UI.Services.Monitoring
{
    public sealed class MonitoringResultListener : BackgroundService
    {
        private readonly IMonitoringResultSubscriber _subscriber;
        private readonly StreamProcessDataProvider _dataProvider;
        private readonly InMemoryHistoryProvider _historyProvider;
        private readonly ILogger<MonitoringResultListener> _logger;

        public MonitoringResultListener(
            IMonitoringResultSubscriber subscriber,
            StreamProcessDataProvider dataProvider,
            InMemoryHistoryProvider historyProvider,
            ILogger<MonitoringResultListener> logger)
        {
            _subscriber = subscriber;
            _dataProvider = dataProvider;
            _historyProvider = historyProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var result in _subscriber.ListenAsync(stoppingToken))
            {
                _dataProvider.Update(result);
                _historyProvider.Add(result);
                _logger.LogDebug("Monitoring result received with {ProcessCount} processes", result.Processes.Count);
            }
        }
    }
}

