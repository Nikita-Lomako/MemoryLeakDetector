namespace MemoryLeakDetector.Core.Abstractions;

// Интерфейс для получения стектрейсов процессов с подозрением на утечку
public interface IStackTraceProvider
{
    // Пытается получить стектрейс для указанного процесса
    // Возвращает текст или null если не удалось
    string? TryCaptureStackTrace(int processId, string processName);
}
