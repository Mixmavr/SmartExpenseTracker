using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Application.Services;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Tests.UnitTests;

public class ExpenseServiceTests
{
    // A fake "database of expenses" that lives in a list.
    private class FakeExpenseRepository : IExpenseRepository
    {
        public List<Expense> Expenses { get; } = new();

        public Task<List<Expense>> GetAllAsync(int userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Expenses.Where(e => e.UserId == userId).ToList());
        }

        public Task<Expense?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Expenses.FirstOrDefault(e => e.Id == id && e.UserId == userId));
        }

        public Task AddAsync(Expense expense, CancellationToken cancellationToken)
        {
            expense.Id = Expenses.Count + 1;
            Expenses.Add(expense);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Expense expense, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Expense expense, CancellationToken cancellationToken)
        {
            Expenses.Remove(expense);
            return Task.CompletedTask;
        }
    }

    // A fake list of categories. The expense service only needs to find a category by id.
    private class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = new();

        public Task<Category?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.FirstOrDefault(c => c.Id == id && c.UserId == userId));
        }

        public Task<List<Category>> GetAllAsync(int userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> NameExistsAsync(int userId, string name, int? excludeId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> HasExpensesAsync(int categoryId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(Category category, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateAsync(Category category, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteAsync(Category category, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    // A fake converter with a fixed rate (1 unit = 0.8 EUR) that counts how many times it is used.
    private class FakeCurrencyConverter : ICurrencyConverter
    {
        public int Calls { get; private set; }

        public Task<ConversionResult> ConvertToEurAsync(decimal amount, string currency, CancellationToken cancellationToken)
        {
            Calls++;

            if (currency == "XXX")
            {
                throw new AppException(ErrorType.Validation, "The currency 'XXX' is not supported.");
            }

            return Task.FromResult(new ConversionResult(Math.Round(amount * 0.8m, 2), 0.8m));
        }
    }

    // Everything the tests need: user 1 has category 1 ("Food"), user 2 has category 2 ("Travel").
    private class Setup
    {
        public FakeExpenseRepository Expenses { get; } = new();
        public FakeCurrencyConverter Converter { get; } = new();
        public ExpenseService Service { get; }

        public Setup()
        {
            var categories = new FakeCategoryRepository();
            categories.Categories.Add(new Category { Id = 1, Name = "Food", UserId = 1 });
            categories.Categories.Add(new Category { Id = 2, Name = "Travel", UserId = 2 });

            Service = new ExpenseService(Expenses, categories, Converter);
        }
    }

    private static ExpenseInput Input(decimal amount = 100m, string currency = "USD", int categoryId = 1, string? description = "Lunch")
    {
        return new ExpenseInput(amount, currency, new DateOnly(2026, 11, 8), description, categoryId);
    }

    // A new expense is converted to EUR and saves the rate it was converted with.
    [Fact]
    public async Task CreateAsync_ConvertsToEurAndSavesTheRate()
    {
        var setup = new Setup();

        var expense = await setup.Service.CreateAsync(1, Input(100m, " usd "), CancellationToken.None);

        Assert.Equal("USD", expense.Currency);
        Assert.Equal(80m, expense.AmountInEur);
        Assert.Equal(0.8m, expense.ExchangeRate);
        Assert.Equal("Food", expense.CategoryName);
        Assert.Equal(1, setup.Expenses.Expenses.Single().UserId);
    }

    // An empty description is saved as null.
    [Fact]
    public async Task CreateAsync_EmptyDescription_IsSavedAsNull()
    {
        var setup = new Setup();

        var expense = await setup.Service.CreateAsync(1, Input(description: "   "), CancellationToken.None);

        Assert.Null(expense.Description);
    }

    // Without a date, the expense gets today's date.
    [Fact]
    public async Task CreateAsync_NoDate_UsesToday()
    {
        var setup = new Setup();
        var input = new ExpenseInput(100m, "USD", null, "Lunch", 1);

        var expense = await setup.Service.CreateAsync(1, input, CancellationToken.None);

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), expense.Date);
    }

    // A date given by the user is saved as it is.
    [Fact]
    public async Task CreateAsync_WithDate_UsesThatDate()
    {
        var setup = new Setup();

        var expense = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 8), expense.Date);
    }

    // Updating without a date keeps the date the expense already had.
    [Fact]
    public async Task UpdateAsync_NoDate_KeepsTheOldDate()
    {
        var setup = new Setup();
        var created = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        var updated = await setup.Service.UpdateAsync(
            1, created.Id, new ExpenseInput(100m, "USD", null, "Dinner", 1), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 8), updated.Date);
        Assert.Equal("Dinner", updated.Description);
    }

    // A category that does not exist is a bad request.
    [Fact]
    public async Task CreateAsync_UnknownCategory_ThrowsValidation()
    {
        var setup = new Setup();

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.CreateAsync(1, Input(categoryId: 99), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    // The category of another user cannot be used, and nothing is saved.
    [Fact]
    public async Task CreateAsync_CategoryOfAnotherUser_ThrowsValidation()
    {
        var setup = new Setup();

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.CreateAsync(1, Input(categoryId: 2), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Empty(setup.Expenses.Expenses);
    }

    // An unsupported currency is rejected and nothing is saved.
    [Fact]
    public async Task CreateAsync_UnsupportedCurrency_ThrowsAndSavesNothing()
    {
        var setup = new Setup();

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.CreateAsync(1, Input(currency: "XXX"), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Empty(setup.Expenses.Expenses);
    }

    // The list contains only the expenses of the user who asks.
    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTheExpensesOfTheUser()
    {
        var setup = new Setup();
        await setup.Service.CreateAsync(1, Input(), CancellationToken.None);
        await setup.Service.CreateAsync(2, Input(categoryId: 2), CancellationToken.None);

        var expenses = await setup.Service.GetAllAsync(1, CancellationToken.None);

        Assert.Single(expenses);
    }

    // An expense of another user looks like it does not exist: NotFound.
    [Fact]
    public async Task GetByIdAsync_ExpenseOfAnotherUser_ThrowsNotFound()
    {
        var setup = new Setup();
        var expense = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.GetByIdAsync(2, expense.Id, CancellationToken.None));

        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    // If only the description changes, the saved rate is kept and the converter is not used again.
    [Fact]
    public async Task UpdateAsync_OnlyTheDescriptionChanges_KeepsTheSavedConversion()
    {
        var setup = new Setup();
        var expense = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        var updated = await setup.Service.UpdateAsync(1, expense.Id, Input(description: "Dinner"), CancellationToken.None);

        Assert.Equal("Dinner", updated.Description);
        Assert.Equal(80m, updated.AmountInEur);
        Assert.Equal(1, setup.Converter.Calls);
    }

    // If the amount changes, the expense is converted again.
    [Fact]
    public async Task UpdateAsync_TheAmountChanges_ConvertsAgain()
    {
        var setup = new Setup();
        var expense = await setup.Service.CreateAsync(1, Input(100m), CancellationToken.None);

        var updated = await setup.Service.UpdateAsync(1, expense.Id, Input(200m), CancellationToken.None);

        Assert.Equal(160m, updated.AmountInEur);
        Assert.Equal(2, setup.Converter.Calls);
    }

    // An expense cannot be moved to the category of another user.
    [Fact]
    public async Task UpdateAsync_CategoryOfAnotherUser_ThrowsValidation()
    {
        var setup = new Setup();
        var expense = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.UpdateAsync(1, expense.Id, Input(categoryId: 2), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    // An expense can be deleted, but not by another user.
    [Fact]
    public async Task DeleteAsync_RemovesTheExpense_AndAnotherUserCannotDeleteIt()
    {
        var setup = new Setup();
        var expense = await setup.Service.CreateAsync(1, Input(), CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.DeleteAsync(2, expense.Id, CancellationToken.None));
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Single(setup.Expenses.Expenses);

        await setup.Service.DeleteAsync(1, expense.Id, CancellationToken.None);
        Assert.Empty(setup.Expenses.Expenses);
    }
}
