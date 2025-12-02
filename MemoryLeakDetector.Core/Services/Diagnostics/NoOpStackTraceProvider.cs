using MemoryLeakDetector.Core.Abstractions;

namespace MemoryLeakDetector.Core.Services.Diagnostics;

/// <summary>
/// Заглушка для получения стектрейсов.
/// На реальном проекте можно заменить на реализацию на основе Microsoft.Diagnostics.NETCore.Client.
/// </summary>
public sealed class NoOpStackTraceProvider : IStackTraceProvider
{
    public string? TryCaptureStackTrace(int processId, string processName)
    {
        // Для курсового проекта достаточно вернуть null или короткое сообщение.
        return null;
    }
}


