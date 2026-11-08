using System.ComponentModel.DataAnnotations;

namespace SmartExpenseTracker.Api.Models;

public class CategoryRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
