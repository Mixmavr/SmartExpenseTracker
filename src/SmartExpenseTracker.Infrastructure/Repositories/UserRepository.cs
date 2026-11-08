using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Domain.Entities;
using SmartExpenseTracker.Infrastructure.Persistence;

namespace SmartExpenseTracker.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
