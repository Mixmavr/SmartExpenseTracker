using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Application.Currency;

namespace SmartExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRatesController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>Returns the latest rates: how many units of each currency equal 1 EUR.</summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetRates(CancellationToken cancellationToken)
    {
        var rates = await _exchangeRateService.GetRatesAsync(cancellationToken);
        return Ok(rates);
    }
}
