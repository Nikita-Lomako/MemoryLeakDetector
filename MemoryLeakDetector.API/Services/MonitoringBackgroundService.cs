using MemoryLeakDetector.API.Services;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.API.Services;

public sealed class MonitoringBackgroundService : BackgroundService
{
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly IMonitoringCoordinator _monitoringCoordinator;
    private readonly IMonitoringResultMapper _resultMapper;
    private readonly IMonitoringHistoryStore _historyStore;
    private readonly MonitoringOptions _options;

    public MonitoringBackgroundService(
        ILogger<MonitoringBackgroundService> logger,
        IMonitoringCoordinator monitoringCoordinator,
        IMonitoringResultMapper resultMapper,
        IMonitoringHistoryStore historyStore,
        IOptions<MonitoringOptions> options)
    {
        _logger = logger;
        _monitoringCoordinator = monitoringCoordinator;
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

                _historyStore.Add(dto);

                _logger.LogInformation(
                    "API monitoring cycle finished in {Duration} — processes: {Processes}, leaks: {Leaks}, errors: {Errors}",
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
                _logger.LogError(ex, "API monitoring cycle failed");
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


