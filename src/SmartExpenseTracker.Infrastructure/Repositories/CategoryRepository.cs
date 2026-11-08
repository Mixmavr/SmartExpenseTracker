using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Domain.Entities;
using SmartExpenseTracker.Infrastructure.Persistence;

namespace SmartExpenseTracker.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Category>> GetAllAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        return await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(int userId, string name, int? excludeId, CancellationToken cancellationToken)
    {
        var lowerName = name.ToLower();

        return await _db.Categories
            .AnyAsync(c => c.UserId == userId && c.Id != excludeId && c.Name.ToLower() == lowerName, cancellationToken);
    }

    public async Task<bool> HasExpensesAsync(int categoryId, CancellationToken cancellationToken)
    {
        return await _db.Expenses.AnyAsync(e => e.CategoryId == categoryId, cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken)
    {
        _db.Categories.Update(category);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Category category, CancellationToken cancellationToken)
    {
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
