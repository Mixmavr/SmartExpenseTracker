namespace SmartExpenseTracker.Application.Models;

public record ExpenseInput(decimal Amount, string Currency, DateOnly? Date, string? Description, int CategoryId);
