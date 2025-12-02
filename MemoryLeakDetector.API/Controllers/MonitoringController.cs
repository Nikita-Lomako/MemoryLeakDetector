using MemoryLeakDetector.API.Services;
using MemoryLeakDetector.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace MemoryLeakDetector.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MonitoringController : ControllerBase
{
    private readonly IMonitoringHistoryStore _historyStore;

    public MonitoringController(IMonitoringHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    /// <summary>
    /// Получить последний результат мониторинга.
    /// </summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(MonitoringResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<MonitoringResultDto> GetLatest()
    {
        var latest = _historyStore.GetLatest();
        if (latest is null)
        {
            return NotFound();
        }

        return Ok(latest);
    }

    /// <summary>
    /// Получить список текущих подозрительных процессов (утечки).
    /// </summary>
    [HttpGet("leaks")]
    [ProducesResponseType(typeof(IEnumerable<LeakInsightDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LeakInsightDto>> GetLeaks([FromQuery] int recentCycles = 5)
    {
        if (recentCycles <= 0)
        {
            recentCycles = 5;
        }

        var allResults = _historyStore.GetRange();
        if (allResults.Count == 0)
        {
            return Ok(Array.Empty<LeakInsightDto>());
        }

        var skip = Math.Max(0, allResults.Count - recentCycles);
        var window = allResults.Skip(skip);

        var leaks = window
            .SelectMany(r => r.Insights)
            .Where(i => i.IsLeakSuspected)
            .OrderByDescending(i => i.WorkingSetGrowthPercent)
            .ThenByDescending(i => i.WorkingSetDeltaMb)
            .ToList();

        return Ok(leaks);
    }
}


