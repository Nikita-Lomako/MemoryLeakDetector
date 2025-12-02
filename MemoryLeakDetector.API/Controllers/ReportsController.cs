using MemoryLeakDetector.API.Models;
using MemoryLeakDetector.API.Services;
using MemoryLeakDetector.API.Services.Reporting;
using MemoryLeakDetector.Core.Contracts;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;

namespace MemoryLeakDetector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController : Controller
{
    private readonly IMonitoringHistoryStore _historyStore;

    public ReportsController(IMonitoringHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    /// <summary>
    /// Получить JSON-отчёт за период.
    /// Если параметры не указаны, возвращаются все доступные записи.
    /// </summary>
    [HttpGet("json")]
    [ProducesResponseType(typeof(IEnumerable<MonitoringResultDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<MonitoringResultDto>> GetJson(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 100)
    {
        if (limit <= 0)
        {
            limit = 100;
        }

        var range = _historyStore.GetRange(from, to);
        var limited = range.Count > limit
            ? range.Skip(range.Count - limit).ToList()
            : range;

        return Ok(limited);
    }

    /// <summary>
    /// HTML-отчёт по мониторингу за период.
    /// </summary>
    [HttpGet("html")]
    [Produces("text/html")]
    public IActionResult GetHtml(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var results = _historyStore.GetRange(from, to);

        var model = new MonitoringReportViewModel
        {
            From = from,
            To = to,
            Results = results
        };

        return View("~/Views/Reports/Report.cshtml", model);
    }

    /// <summary>
    /// PDF-отчёт по мониторингу за период.
    /// </summary>
    [HttpGet("pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetPdf(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 100)
    {
        if (limit <= 0)
        {
            limit = 100;
        }

        var range = _historyStore.GetRange(from, to);
        var results = range.Count > limit
            ? range.Skip(range.Count - limit).ToList()
            : range;

        var model = new MonitoringReportViewModel
        {
            From = from,
            To = to,
            Results = results
        };

        var document = new MonitoringReportDocument(model);
        var pdfBytes = document.GeneratePdf();

        var fileName = $"memory-report-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}


