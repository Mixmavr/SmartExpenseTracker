using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IExpenseRepository
{
    Task<List<Expense>> GetAllAsync(int userId, ExpenseFilter filter, CancellationToken cancellationToken);

    Task<Expense?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken);

    Task AddAsync(Expense expense, CancellationToken cancellationToken);

    Task UpdateAsync(Expense expense, CancellationToken cancellationToken);

    Task DeleteAsync(Expense expense, CancellationToken cancellationToken);
}
