using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statistics")]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statistics;

    public StatisticsController(IStatisticsService statistics)
    {
        _statistics = statistics;
    }

    /// <summary>In-memory statistics since the application started: requests, and exchange rate cache and API usage.</summary>
    [HttpGet]
    public ActionResult<StatisticsDto> Get()
    {
        return Ok(_statistics.GetSnapshot());
    }
}
