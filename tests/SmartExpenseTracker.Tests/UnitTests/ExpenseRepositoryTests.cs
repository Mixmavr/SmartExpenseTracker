using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Domain.Entities;
using SmartExpenseTracker.Infrastructure.Persistence;
using SmartExpenseTracker.Infrastructure.Repositories;

namespace SmartExpenseTracker.Tests.UnitTests;

// These tests use a real SQLite database in memory, because the filters are database queries.
public class ExpenseRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ExpenseRepository _repository;

    // User 1 has 4 expenses (ids 1 to 4). User 2 has 1 expense (id 5).
    // The amounts have different lengths on purpose, to check that money is sorted as numbers (the EUR values are not real conversions).
    public ExpenseRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.AddRange(
            new User { Id = 1, Email = "one@example.com", PasswordHash = "x" },
            new User { Id = 2, Email = "two@example.com", PasswordHash = "x" });

        _db.Categories.AddRange(
            new Category { Id = 1, Name = "Food", UserId = 1 },
            new Category { Id = 2, Name = "Transport", UserId = 1 },
            new Category { Id = 3, Name = "Travel", UserId = 2 });

        _db.Expenses.AddRange(
            NewExpense(1, 1, 1, new DateOnly(2026, 8, 10), 12.80m, "EUR", 11.47m),
            NewExpense(2, 1, 1, new DateOnly(2026, 9, 10), 2500m, "USD", 45.20m),
            NewExpense(3, 1, 2, new DateOnly(2026, 9, 20), 45.20m, "EUR", 30m),
            NewExpense(4, 1, 1, new DateOnly(2026, 10, 5), 24m, "GBP", 28.76m),
            NewExpense(5, 2, 3, new DateOnly(2026, 9, 15), 999m, "EUR", 999m));

        _db.SaveChanges();
        _repository = new ExpenseRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static Expense NewExpense(int id, int userId, int categoryId, DateOnly date, decimal amount, string currency, decimal amountInEur)
    {
        return new Expense
        {
            Id = id,
            UserId = userId,
            CategoryId = categoryId,
            Date = date,
            Amount = amount,
            Currency = currency,
            AmountInEur = amountInEur,
            ExchangeRate = 1m,
        };
    }

    private async Task<List<int>> IdsAsync(int userId, ExpenseFilter filter)
    {
        var expenses = await _repository.GetAllAsync(userId, filter, CancellationToken.None);
        return expenses.Select(e => e.Id).ToList();
    }

    // With no filters we get all the expenses of the user, newest first, and never those of another user.
    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsTheUsersExpensesNewestFirst()
    {
        var ids = await IdsAsync(1, new ExpenseFilter());

        Assert.Equal(new[] { 4, 3, 2, 1 }, ids);
    }

    // The category name is loaded with the expense.
    [Fact]
    public async Task GetAllAsync_LoadsTheCategory()
    {
        var expenses = await _repository.GetAllAsync(1, new ExpenseFilter(), CancellationToken.None);

        Assert.All(expenses, e => Assert.NotNull(e.Category));
    }

    // Filter by category.
    [Fact]
    public async Task GetAllAsync_FilterByCategory()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(CategoryId: 2));

        Assert.Equal(new[] { 3 }, ids);
    }

    // A category of another user gives no results, not an error.
    [Fact]
    public async Task GetAllAsync_CategoryOfAnotherUser_ReturnsNothing()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(CategoryId: 3));

        Assert.Empty(ids);
    }

    // Filter by currency.
    [Fact]
    public async Task GetAllAsync_FilterByCurrency()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(Currency: "EUR"));

        Assert.Equal(new[] { 3, 1 }, ids);
    }

    // The date range includes both the 'from' and the 'to' day.
    [Fact]
    public async Task GetAllAsync_FilterByDateRange_IncludesBothEnds()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(From: new DateOnly(2026, 9, 10), To: new DateOnly(2026, 10, 5)));

        Assert.Equal(new[] { 4, 3, 2 }, ids);
    }

    // Only a 'from' date, or only a 'to' date, also works.
    [Fact]
    public async Task GetAllAsync_OnlyFromOrOnlyTo()
    {
        var fromIds = await IdsAsync(1, new ExpenseFilter(From: new DateOnly(2026, 9, 20)));
        var toIds = await IdsAsync(1, new ExpenseFilter(To: new DateOnly(2026, 9, 10)));

        Assert.Equal(new[] { 4, 3 }, fromIds);
        Assert.Equal(new[] { 2, 1 }, toIds);
    }

    // Filters can be combined.
    [Fact]
    public async Task GetAllAsync_CombinedFilters()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(CategoryId: 1, Currency: "USD", From: new DateOnly(2026, 9, 1)));

        Assert.Equal(new[] { 2 }, ids);
    }

    // Sort by amount, ascending.
    [Fact]
    public async Task GetAllAsync_SortByAmountAscending()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(SortBy: ExpenseSortField.Amount, Direction: SortDirection.Asc));

        Assert.Equal(new[] { 1, 4, 3, 2 }, ids);
    }

    // Sort by the amount in EUR, descending.
    [Fact]
    public async Task GetAllAsync_SortByAmountInEurDescending()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(SortBy: ExpenseSortField.AmountInEur, Direction: SortDirection.Desc));

        Assert.Equal(new[] { 2, 3, 4, 1 }, ids);
    }

    // Sort by date, oldest first.
    [Fact]
    public async Task GetAllAsync_SortByDateAscending()
    {
        var ids = await IdsAsync(1, new ExpenseFilter(SortBy: ExpenseSortField.Date, Direction: SortDirection.Asc));

        Assert.Equal(new[] { 1, 2, 3, 4 }, ids);
    }
}
