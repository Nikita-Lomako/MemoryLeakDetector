using System.Runtime.Versioning;
using MemoryLeakDetector.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace MemoryLeakDetector.Core.Services.Diagnostics;

// Провайдер информации об утечках - возвращает рекомендации по анализу
[SupportedOSPlatform("windows")]
public sealed class DotNetDiagnosticsStackTraceProvider : IStackTraceProvider
{
    private readonly ILogger<DotNetDiagnosticsStackTraceProvider> _logger;

    public DotNetDiagnosticsStackTraceProvider(ILogger<DotNetDiagnosticsStackTraceProvider> logger)
    {
        _logger = logger;
    }

    // Возвращает информацию и рекомендации для процесса с утечкой
    public string? TryCaptureStackTrace(int processId, string processName)
    {
        try
        {
            _logger.LogDebug(
                "Memory leak detected for {ProcessName} (PID {ProcessId}). Stack trace analysis available via external tools.",
                processName, processId);

            return 
                $"Обнаружена утечка памяти в процессе '{processName}' (PID {processId}).\n\n" +
                "Для детального анализа стека вызовов используйте:\n" +
                "• Visual Studio → Debug → Attach to Process → выберите процесс\n" +
                "• Visual Studio → Debug → Take Snapshot (для .NET процессов)\n" +
                "• PerfView для анализа GC и памяти\n" +
                "• dotnet-counters для мониторинга .NET метрик\n";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error generating stack trace info for {ProcessName} ({ProcessId})", processName, processId);
            return null;
        }
    }
}
