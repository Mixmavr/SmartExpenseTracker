using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Domain.Entities;
using SmartExpenseTracker.Infrastructure.Persistence;

namespace SmartExpenseTracker.Infrastructure.Repositories;

public class ExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;

    public ExpenseRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Expense>> GetAllAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Expense?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        return await _db.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Expense expense, CancellationToken cancellationToken)
    {
        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
