using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Mappers;

public static class ExpenseMapper
{
    // The expense must be loaded together with its category (it uses the category name).
    public static ExpenseDto ToDto(this Expense expense)
    {
        return new ExpenseDto(
            expense.Id,
            expense.Amount,
            expense.Currency,
            expense.Date,
            expense.Description,
            expense.ExchangeRate,
            expense.AmountInEur,
            expense.CategoryId,
            expense.Category!.Name);
    }
}
