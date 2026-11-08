using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ICurrencyConverter _currencyConverter;

    public ExchangeRatesController(IExchangeRateService exchangeRateService, ICurrencyConverter currencyConverter)
    {
        _exchangeRateService = exchangeRateService;
        _currencyConverter = currencyConverter;
    }

    /// <summary>Returns the latest rates: how many units of each currency equal 1 EUR.</summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetRates(CancellationToken cancellationToken)
    {
        var rates = await _exchangeRateService.GetRatesAsync(cancellationToken);
        return Ok(rates);
    }

    /// <summary>Converts an amount to EUR</summary>
    [HttpGet("convert")]
    public async Task<ActionResult<ConversionResult>> Convert(decimal amount, string currency, CancellationToken cancellationToken)
    {
        var result = await _currencyConverter.ConvertToEurAsync(amount, currency, cancellationToken);
        return Ok(result);
    }
}
