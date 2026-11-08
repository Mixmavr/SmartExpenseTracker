namespace SmartExpenseTracker.Application.Models;

public record ExpenseDto(
    int Id,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string? Description,
    decimal ExchangeRate,
    decimal AmountInEur,
    int CategoryId,
    string CategoryName);
