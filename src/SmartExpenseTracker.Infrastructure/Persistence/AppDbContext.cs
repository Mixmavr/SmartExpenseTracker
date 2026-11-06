using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Users: the email must be unique.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Categories: each user has their own categories, with unique names.
        modelBuilder.Entity<Category>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId);

        modelBuilder.Entity<Category>()
            .HasIndex(c => new { c.UserId, c.Name })
            .IsUnique();

        // Expenses: money columns and relations.
        modelBuilder.Entity<Expense>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Expense>()
            .Property(e => e.AmountInEur)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Expense>()
            .Property(e => e.ExchangeRate)
            .HasPrecision(18, 8);

        modelBuilder.Entity<Expense>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId);

        // A category that still has expenses cannot be deleted.
        modelBuilder.Entity<Expense>()
            .HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for the filters we will need: date range, category, currency.
        modelBuilder.Entity<Expense>()
            .HasIndex(e => new { e.UserId, e.Date });

        modelBuilder.Entity<Expense>()
            .HasIndex(e => new { e.UserId, e.CategoryId });

        modelBuilder.Entity<Expense>()
            .HasIndex(e => new { e.UserId, e.Currency });
    }
}
