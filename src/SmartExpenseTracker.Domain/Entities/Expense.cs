namespace SmartExpenseTracker.Domain.Entities;

public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }

    // Saved at creation time so old expenses do not change when the rates change.
    public decimal ExchangeRate { get; set; }
    public decimal AmountInEur { get; set; }

    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
