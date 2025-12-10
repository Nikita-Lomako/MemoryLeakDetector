using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

/// <summary>
/// Интерфейс для генерации отчетов в различных форматах.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Генерирует JSON-отчет.
    /// </summary>
    /// <param name="model">Модель данных отчета.</param>
    /// <param name="limit">Максимальное количество записей.</param>
    /// <returns>JSON-строку с данными отчета.</returns>
    string GenerateJson(MonitoringReportModel model, int limit = 100);

    /// <summary>
    /// Генерирует HTML-отчет.
    /// </summary>
    /// <param name="model">Модель данных отчета.</param>
    /// <returns>HTML-строку с отчетом.</returns>
    string GenerateHtml(MonitoringReportModel model);

    /// <summary>
    /// Генерирует PDF-отчет.
    /// </summary>
    /// <param name="model">Модель данных отчета.</param>
    /// <param name="limit">Максимальное количество записей.</param>
    /// <returns>Массив байтов PDF-файла.</returns>
    byte[] GeneratePdf(MonitoringReportModel model, int limit = 100);
}

