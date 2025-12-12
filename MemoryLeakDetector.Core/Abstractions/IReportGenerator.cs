using MemoryLeakDetector.Core.Models;

namespace MemoryLeakDetector.Core.Abstractions;

// Генератор отчетов в разных форматах
public interface IReportGenerator
{
    // JSON отчет
    string GenerateJson(MonitoringReportModel model, int limit = 100);

    // HTML отчет
    string GenerateHtml(MonitoringReportModel model);

    // PDF отчет (возвращает байты файла)
    byte[] GeneratePdf(MonitoringReportModel model, int limit = 100);
}
