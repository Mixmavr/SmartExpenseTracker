namespace SmartExpenseTracker.Application.Models;

public record ExpenseFilter(
    int? CategoryId = null,
    string? Currency = null,
    DateOnly? From = null,
    DateOnly? To = null,
    ExpenseSortField SortBy = ExpenseSortField.Date,
    SortDirection Direction = SortDirection.Desc);
