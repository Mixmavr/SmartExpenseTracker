namespace SmartExpenseTracker.Application.Models;

// PeriodStart is the day itself, the Monday of the week, or the first day of the month.
public record PeriodTotalDto(DateOnly PeriodStart, int Count, decimal TotalEur);
