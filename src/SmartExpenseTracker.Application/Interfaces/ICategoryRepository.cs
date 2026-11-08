using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(int userId, CancellationToken cancellationToken);

    Task<Category?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);

    // excludeId lets a category keep its own name when it is updated.
    Task<bool> NameExistsAsync(int userId, string name, int? excludeId, CancellationToken cancellationToken);

    Task<bool> HasExpensesAsync(int categoryId, CancellationToken cancellationToken);

    Task AddAsync(Category category, CancellationToken cancellationToken);

    Task UpdateAsync(Category category, CancellationToken cancellationToken);

    Task DeleteAsync(Category category, CancellationToken cancellationToken);
}
