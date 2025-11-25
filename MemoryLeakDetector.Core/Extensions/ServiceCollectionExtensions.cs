using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Core.Services.Baselines;
using MemoryLeakDetector.Core.Services.Detection;
using MemoryLeakDetector.Core.Services.Metrics;
using MemoryLeakDetector.Core.Services.Monitoring;
using MemoryLeakDetector.Core.Services.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MemoryLeakDetector.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryLeakDetectorCore(
        this IServiceCollection services,
        Action<MonitoringOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.TryAddSingleton<IBaselineRepository, InMemoryBaselineRepository>();
        services.TryAddSingleton<IProcessMetricsCollector, ProcessMetricsCollector>();
        services.TryAddSingleton<ILeakDetectionStrategy, ThresholdLeakDetectionStrategy>();
        services.TryAddSingleton<IMonitoringCoordinator, MonitoringCoordinator>();
        services.TryAddSingleton<IMonitoringResultStream, InMemoryMonitoringResultStream>();
        services.TryAddSingleton<IMonitoringResultMapper, MonitoringResultMapper>();

        return services;
    }
}

