using System.ComponentModel.DataAnnotations;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Api.Models;

public class ExpenseQuery
{
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "The currency must be a 3-letter code, for example USD.")]
    public string? Currency { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public ExpenseSortField SortBy { get; set; } = ExpenseSortField.Date;

    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}
