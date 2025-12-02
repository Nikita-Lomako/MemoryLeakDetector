using System.IO;
using System.Runtime.Versioning;
using MemoryLeakDetector.Core.Abstractions;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Extensions.Logging;

namespace MemoryLeakDetector.Core.Services.Diagnostics;

/// <summary>
/// Реализация IStackTraceProvider на основе Microsoft.Diagnostics.NETCore.Client.
/// Для упрощения в рамках курсового проекта делает управляемый дамп процесса,
/// который можно открыть в Visual Studio или dotnet-dump и посмотреть стеки вызовов.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DotNetDiagnosticsStackTraceProvider : IStackTraceProvider
{
    private readonly ILogger<DotNetDiagnosticsStackTraceProvider> _logger;

    public DotNetDiagnosticsStackTraceProvider(ILogger<DotNetDiagnosticsStackTraceProvider> logger)
    {
        _logger = logger;
    }

    public string? TryCaptureStackTrace(int processId, string processName)
    {
        try
        {
            var client = new DiagnosticsClient(processId);

            var safeName = string.IsNullOrWhiteSpace(processName)
                ? "process"
                : string.Concat(processName.Split(Path.GetInvalidFileNameChars()));

            var fileName = $"MemoryLeakDetector_{safeName}_{processId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dmp";
            var dumpPath = Path.Combine(Path.GetTempPath(), fileName);

            // Дамп с управляемой кучей — достаточно для анализа стеков и утечек в .NET-процессах.
            client.WriteDump(DumpType.WithHeap, dumpPath, logDumpGeneration: false);

            _logger.LogInformation("Managed dump for {ProcessName} ({ProcessId}) written to {Path}", processName, processId, dumpPath);

            return
                $"Managed memory dump created for process '{processName}' (PID {processId}) at:\n" +
                $"{dumpPath}\n" +
                "Open this dump in Visual Studio, WinDbg or use 'dotnet-dump analyze' to inspect call stacks and memory usage.";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create managed dump for {ProcessName} ({ProcessId})", processName, processId);
            return null;
        }
    }
}


