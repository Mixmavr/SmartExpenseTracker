using System.ComponentModel.DataAnnotations;

namespace SmartExpenseTracker.Api.Models;

public class ExpenseRequest
{
    [Range(0.01, 1000000000)]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "The currency must be a 3-letter code, for example USD.")]
    public string Currency { get; set; } = string.Empty;

    // Optional: when missing, the expense gets today's date.
    public DateOnly? Date { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}
