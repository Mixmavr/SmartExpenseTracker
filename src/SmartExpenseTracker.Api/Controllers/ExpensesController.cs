using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Api.Extensions;
using SmartExpenseTracker.Api.Models;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>Returns the expenses of the logged-in user, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var expenses = await _expenseService.GetAllAsync(User.GetUserId(), cancellationToken);
        return Ok(expenses);
    }

    /// <summary>Returns one expense.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.GetByIdAsync(User.GetUserId(), id, cancellationToken);
        return Ok(expense);
    }

    /// <summary>Creates an expense and converts its amount to EUR.</summary>
    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(ExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.CreateAsync(User.GetUserId(), ToInput(request), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
    }

    /// <summary>Updates an expense. It is converted again only if the amount or currency changed.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExpenseDto>> Update(int id, ExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await _expenseService.UpdateAsync(User.GetUserId(), id, ToInput(request), cancellationToken);
        return Ok(expense);
    }

    /// <summary>Deletes an expense.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _expenseService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private static ExpenseInput ToInput(ExpenseRequest request)
    {
        return new ExpenseInput(request.Amount, request.Currency, request.Date, request.Description, request.CategoryId);
    }
}
