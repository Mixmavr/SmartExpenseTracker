namespace SmartExpenseTracker.Application.Models;

public record CategoryTotalDto(int CategoryId, string CategoryName, int Count, decimal TotalEur);
