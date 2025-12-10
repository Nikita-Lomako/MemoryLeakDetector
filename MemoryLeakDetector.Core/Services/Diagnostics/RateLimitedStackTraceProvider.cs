using System.Collections.Concurrent;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace MemoryLeakDetector.Core.Services.Diagnostics;

/// <summary>
/// Обертка над IStackTraceProvider с rate limiting для предотвращения блокировки системы.
/// Ограничивает частоту создания dump файлов для каждого процесса.
/// </summary>
public sealed class RateLimitedStackTraceProvider : IStackTraceProvider
{
    private readonly IStackTraceProvider _innerProvider;
    private readonly MonitoringOptions _options;
    private readonly ILogger<RateLimitedStackTraceProvider> _logger;
    private readonly ConcurrentDictionary<int, DateTime> _lastDumpTimes = new();

    public RateLimitedStackTraceProvider(
        IStackTraceProvider innerProvider,
        IOptions<MonitoringOptions> options,
        ILogger<RateLimitedStackTraceProvider> logger)
    {
        _innerProvider = innerProvider;
        _options = options.Value;
        _logger = logger;
    }

    public string? TryCaptureStackTrace(int processId, string processName)
    {
        // Проверяем rate limiting
        if (!ShouldCreateDump(processId))
        {
            _logger.LogDebug(
                "Skipping dump creation for {ProcessName} ({ProcessId}) - rate limit not exceeded (min interval: {Interval}s)",
                processName, processId, _options.DumpCreationMinIntervalSeconds);
            return null;
        }

        try
        {
            var result = _innerProvider.TryCaptureStackTrace(processId, processName);
            if (result != null)
            {
                _lastDumpTimes[processId] = DateTime.UtcNow;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture stack trace for {ProcessName} ({ProcessId})", processName, processId);
            return null;
        }
    }

    private bool ShouldCreateDump(int processId)
    {
        // Если значение -1, dump файлы полностью отключены
        if (_options.DumpCreationMinIntervalSeconds < 0)
        {
            return false;
        }

        // Если значение 0, rate limiting отключен
        if (_options.DumpCreationMinIntervalSeconds == 0)
        {
            return true;
        }

        if (!_lastDumpTimes.TryGetValue(processId, out var lastDumpTime))
        {
            return true; // Первый dump для этого процесса
        }

        var elapsed = (DateTime.UtcNow - lastDumpTime).TotalSeconds;
        return elapsed >= _options.DumpCreationMinIntervalSeconds;
    }

    /// <summary>
    /// Очищает историю для неактивных процессов.
    /// </summary>
    public void PruneInactive(IEnumerable<int> activeProcessIds)
    {
        var activeSet = new HashSet<int>(activeProcessIds);
        
        foreach (var key in _lastDumpTimes.Keys)
        {
            if (!activeSet.Contains(key))
            {
                _lastDumpTimes.TryRemove(key, out _);
            }
        }
    }
}

