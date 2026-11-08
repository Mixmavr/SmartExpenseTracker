using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(int userId, CancellationToken cancellationToken);

    Task<CategoryDto> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(int userId, string name, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(int userId, int id, string name, CancellationToken cancellationToken);

    Task DeleteAsync(int userId, int id, CancellationToken cancellationToken);
}
