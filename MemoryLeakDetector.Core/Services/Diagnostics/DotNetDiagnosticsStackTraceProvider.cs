using System.Diagnostics;
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
/// Использует создание dump через dotnet-dump для избежания блокировки UI.
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
            var safeName = string.IsNullOrWhiteSpace(processName)
                ? "process"
                : string.Concat(processName.Split(Path.GetInvalidFileNameChars()));

            var fileName = $"MemoryLeakDetector_{safeName}_{processId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dmp";
            var dumpPath = Path.Combine(Path.GetTempPath(), fileName);

            // Пытаемся использовать dotnet-dump через Process.Start для асинхронного создания без блокировки UI
            // Это предотвращает появление консоли и блокировку основного потока
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"dump collect -p {processId} --type heap -o \"{dumpPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start dotnet-dump process");
                }

                // Ждем завершения, но с таймаутом (максимум 30 секунд)
                var completed = process.WaitForExit(30000);
                if (!completed)
                {
                    try
                    {
                        process.Kill();
                        _logger.LogWarning("dotnet-dump process timeout for {ProcessName} ({ProcessId})", processName, processId);
                    }
                    catch
                    {
                        // Ignore
                    }
                    return null;
                }

                if (process.ExitCode != 0 || !File.Exists(dumpPath))
                {
                    var error = process.StandardError.ReadToEnd();
                    _logger.LogDebug("dotnet-dump failed for {ProcessName} ({ProcessId}): Exit code {ExitCode}, Error: {Error}", 
                        processName, processId, process.ExitCode, error);
                    throw new InvalidOperationException($"dotnet-dump failed with exit code {process.ExitCode}");
                }

                _logger.LogInformation("Managed dump for {ProcessName} ({ProcessId}) written to {Path}", processName, processId, dumpPath);

                return
                    $"Managed memory dump created for process '{processName}' (PID {processId}) at:\n" +
                    $"{dumpPath}\n" +
                    "Open this dump in Visual Studio, WinDbg or use 'dotnet-dump analyze' to inspect call stacks and memory usage.";
            }
            catch (Exception ex)
            {
                // Если dotnet-dump недоступен, просто возвращаем null
                // Не используем fallback, чтобы не показывать консоль и не блокировать UI
                _logger.LogDebug(ex, "dotnet-dump not available for {ProcessName} ({ProcessId}), skipping dump creation", processName, processId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create managed dump for {ProcessName} ({ProcessId})", processName, processId);
            return null;
        }
    }
    
}


