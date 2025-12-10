using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMonitoringCoordinator _monitoringCoordinator;
        private readonly IMonitoringResultStream _resultStream;
        private readonly IMonitoringResultMapper _resultMapper;
        private readonly IMonitoringHistoryStore _historyStore;
        private readonly MonitoringOptions _options;

        public Worker(
            ILogger<Worker> logger,
            IMonitoringCoordinator monitoringCoordinator,
            IMonitoringResultStream resultStream,
            IMonitoringResultMapper resultMapper,
            IMonitoringHistoryStore historyStore,
            IOptions<MonitoringOptions> options)
        {
            _logger = logger;
            _monitoringCoordinator = monitoringCoordinator;
            _resultStream = resultStream;
            _resultMapper = resultMapper;
            _historyStore = historyStore;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = TimeSpan.FromMilliseconds(Math.Max(250, _options.PollingIntervalMilliseconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _monitoringCoordinator.RunCycleAsync(stoppingToken);
                    var dto = _resultMapper.Map(result);
                    
                    // Публикуем в поток для подписчиков (например, через Named Pipe для UI)
                    await _resultStream.PublishAsync(dto, stoppingToken);
                    
                    // Сохраняем в историю для последующего анализа и отчетов
                    _historyStore.Add(dto);

                    _logger.LogInformation(
                        "Monitoring cycle finished in {Duration} — processes: {Processes}, leaks: {Leaks}, errors: {Errors}",
                        result.Duration,
                        result.ActiveProcessCount,
                        result.LeakSuspicions,
                        result.ErrorCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monitoring cycle failed");
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
