using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Core.Services.Baselines;
using MemoryLeakDetector.Core.Services.Detection;
using MemoryLeakDetector.Core.Services.Diagnostics;
using MemoryLeakDetector.Core.Services.History;
using MemoryLeakDetector.Core.Services.Metrics;
using MemoryLeakDetector.Core.Services.Monitoring;
using MemoryLeakDetector.Core.Services.Streaming;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MemoryLeakDetector.Core.Extensions;

// Расширение для регистрации сервисов ядра в DI контейнере
[SupportedOSPlatform("windows")]
public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddMemoryLeakDetectorCore(
        this IServiceCollection services,
        Action<MonitoringOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.TryAddSingleton<IBaselineRepository, InMemoryBaselineRepository>();
        services.TryAddSingleton<IGcMetricsProvider, DotNetGcMetricsProvider>();
        services.TryAddSingleton<IProcessMetricsCollector, ProcessMetricsCollector>();
        
        // Трекер подозрений для проверки устойчивости утечек
        services.TryAddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MonitoringOptions>>();
            return new LeakSuspicionTracker(options.Value.LeakConfirmationCycles);
        });
        
        services.TryAddSingleton<ThresholdLeakDetectionStrategy>();
        
        // Провайдер информации о процессах
        services.TryAddSingleton<IStackTraceProvider, DotNetDiagnosticsStackTraceProvider>();
        
        services.TryAddSingleton<IMonitoringCoordinator, MonitoringCoordinator>();
        services.TryAddSingleton<IMonitoringResultStream, InMemoryMonitoringResultStream>();
        services.TryAddSingleton<IMonitoringResultMapper, MonitoringResultMapper>();
        services.TryAddSingleton<IMonitoringHistoryStore, InMemoryMonitoringHistoryStore>();

        return services;
    }
}
