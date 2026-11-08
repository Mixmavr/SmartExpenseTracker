using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;
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

    public async Task<List<Expense>> GetAllAsync(int userId, ExpenseFilter filter, CancellationToken cancellationToken)
    {
        // Always filter by user first, so nobody can see the expenses of another user.
        var query = _db.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId);

        if (filter.CategoryId != null)
        {
            var categoryId = filter.CategoryId.Value;
            query = query.Where(e => e.CategoryId == categoryId);
        }

        if (filter.Currency != null)
        {
            var currency = filter.Currency;
            query = query.Where(e => e.Currency == currency);
        }

        if (filter.From != null)
        {
            var from = filter.From.Value;
            query = query.Where(e => e.Date >= from);
        }

        if (filter.To != null)
        {
            var to = filter.To.Value;
            query = query.Where(e => e.Date <= to);
        }

        var descending = filter.Direction == SortDirection.Desc;

        IOrderedQueryable<Expense> ordered = (filter.SortBy, descending) switch
        {
            (ExpenseSortField.Amount, false) => query.OrderBy(e => (double)e.Amount),
            (ExpenseSortField.Amount, true) => query.OrderByDescending(e => (double)e.Amount),
            (ExpenseSortField.AmountInEur, false) => query.OrderBy(e => (double)e.AmountInEur),
            (ExpenseSortField.AmountInEur, true) => query.OrderByDescending(e => (double)e.AmountInEur),
            (_, false) => query.OrderBy(e => e.Date),
            _ => query.OrderByDescending(e => e.Date),
        };

        // The id keeps the order stable when two expenses have the same value.
        ordered = descending ? ordered.ThenByDescending(e => e.Id) : ordered.ThenBy(e => e.Id);

        return await ordered.ToListAsync(cancellationToken);
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
