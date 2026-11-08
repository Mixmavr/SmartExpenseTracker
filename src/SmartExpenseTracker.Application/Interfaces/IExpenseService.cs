using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IExpenseService
{
    Task<List<ExpenseDto>> GetAllAsync(int userId, ExpenseFilter filter, CancellationToken cancellationToken);

    Task<ExpenseDto> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);

    Task<ExpenseDto> CreateAsync(int userId, ExpenseInput input, CancellationToken cancellationToken);

    Task<ExpenseDto> UpdateAsync(int userId, int id, ExpenseInput input, CancellationToken cancellationToken);

    Task DeleteAsync(int userId, int id, CancellationToken cancellationToken);

    Task<List<CategoryTotalDto>> GetTotalsByCategoryAsync(int userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    Task<List<PeriodTotalDto>> GetTotalsByPeriodAsync(int userId, DateOnly? from, DateOnly? to, SummaryPeriod period, CancellationToken cancellationToken);
}
