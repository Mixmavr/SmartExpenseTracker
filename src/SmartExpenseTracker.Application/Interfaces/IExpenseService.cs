using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IExpenseService
{
    Task<List<ExpenseDto>> GetAllAsync(int userId, CancellationToken cancellationToken);

    Task<ExpenseDto> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);

    Task<ExpenseDto> CreateAsync(int userId, ExpenseInput input, CancellationToken cancellationToken);

    Task<ExpenseDto> UpdateAsync(int userId, int id, ExpenseInput input, CancellationToken cancellationToken);

    Task DeleteAsync(int userId, int id, CancellationToken cancellationToken);
}
