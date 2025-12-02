namespace MemoryLeakDetector.Core.Abstractions;

/// <summary>
/// Отвечает за получение стектрейсов для подозрительных процессов.
/// Базовая реализация может быть заглушкой; позже можно использовать Microsoft.Diagnostics.NETCore.Client / EventPipe.
/// </summary>
public interface IStackTraceProvider
{
    /// <summary>
    /// Попробовать получить стектрейс для процесса.
    /// </summary>
    /// <param name="processId">ИД процесса.</param>
    /// <param name="processName">Имя процесса (для логов/диагностики).</param>
    /// <returns>Читаемый текст стектрейса или null, если получить не удалось.</returns>
    string? TryCaptureStackTrace(int processId, string processName);
}


