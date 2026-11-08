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

        // The last filter the service asked for, so tests can check what reached the repository.
        public ExpenseFilter? LastFilter { get; private set; }

        public Task<List<Expense>> GetAllAsync(int userId, ExpenseFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;

            var result = Expenses
                .Where(e => e.UserId == userId)
                .Where(e => filter.From == null || e.Date >= filter.From)
                .Where(e => filter.To == null || e.Date <= filter.To)
                .ToList();

            return Task.FromResult(result);
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

        var expenses = await setup.Service.GetAllAsync(1, new ExpenseFilter(), CancellationToken.None);

        Assert.Single(expenses);
    }

    // A 'from' date after the 'to' date is a bad request.
    [Fact]
    public async Task GetAllAsync_FromAfterTo_ThrowsValidation()
    {
        var setup = new Setup();
        var filter = new ExpenseFilter(From: new DateOnly(2026, 11, 1), To: new DateOnly(2026, 10, 1));

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.GetAllAsync(1, filter, CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    // The currency filter is cleaned (upper case, no spaces) before it reaches the repository. A blank one is ignored.
    [Fact]
    public async Task GetAllAsync_CurrencyFilter_IsCleaned()
    {
        var setup = new Setup();

        await setup.Service.GetAllAsync(1, new ExpenseFilter(Currency: " usd "), CancellationToken.None);
        Assert.Equal("USD", setup.Expenses.LastFilter!.Currency);

        await setup.Service.GetAllAsync(1, new ExpenseFilter(Currency: "   "), CancellationToken.None);
        Assert.Null(setup.Expenses.LastFilter!.Currency);
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

    // For the totals: user 1 has Food 11.47 (Mon 5 Oct), Food 45.20 (Wed 7 Oct), Transport 28.76 (Sun 11 Oct)
    // and Transport 30.00 (Mon 2 Nov). User 2 has one expense that must never be counted.
    // The amounts have decimals on purpose, to check that the sums are exact.
    private static Setup SetupWithExpensesForTotals()
    {
        var setup = new Setup();
        var food = new Category { Id = 1, Name = "Food", UserId = 1 };
        var transport = new Category { Id = 3, Name = "Transport", UserId = 1 };
        var travel = new Category { Id = 2, Name = "Travel", UserId = 2 };

        setup.Expenses.Expenses.Add(NewExpenseForTotals(1, 1, food, new DateOnly(2026, 10, 5), 11.47m));
        setup.Expenses.Expenses.Add(NewExpenseForTotals(2, 1, food, new DateOnly(2026, 10, 7), 45.20m));
        setup.Expenses.Expenses.Add(NewExpenseForTotals(3, 1, transport, new DateOnly(2026, 10, 11), 28.76m));
        setup.Expenses.Expenses.Add(NewExpenseForTotals(4, 1, transport, new DateOnly(2026, 11, 2), 30.00m));
        setup.Expenses.Expenses.Add(NewExpenseForTotals(5, 2, travel, new DateOnly(2026, 10, 6), 999m));

        return setup;
    }

    private static Expense NewExpenseForTotals(int id, int userId, Category category, DateOnly date, decimal amountInEur)
    {
        return new Expense { Id = id, UserId = userId, CategoryId = category.Id, Category = category, Date = date, AmountInEur = amountInEur };
    }

    // The totals per category, with the biggest first. The expense of the other user is not counted.
    [Fact]
    public async Task GetTotalsByCategoryAsync_GroupsByCategory_BiggestFirst()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByCategoryAsync(1, null, null, CancellationToken.None);

        Assert.Equal(
            new[] { new CategoryTotalDto(3, "Transport", 2, 58.76m), new CategoryTotalDto(1, "Food", 2, 56.67m) },
            totals);
    }

    // Only the expenses between 'from' and 'to' are counted.
    [Fact]
    public async Task GetTotalsByCategoryAsync_UsesTheDateRange()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByCategoryAsync(1, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 31), CancellationToken.None);

        Assert.Equal(
            new[] { new CategoryTotalDto(1, "Food", 1, 45.20m), new CategoryTotalDto(3, "Transport", 1, 28.76m) },
            totals);
    }

    // A user without expenses gets an empty list.
    [Fact]
    public async Task GetTotalsByCategoryAsync_NoExpenses_ReturnsAnEmptyList()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByCategoryAsync(99, null, null, CancellationToken.None);

        Assert.Empty(totals);
    }

    // A 'from' date after the 'to' date is a bad request.
    [Fact]
    public async Task GetTotalsByCategoryAsync_FromAfterTo_ThrowsValidation()
    {
        var setup = SetupWithExpensesForTotals();

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.GetTotalsByCategoryAsync(1, new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1), CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
    }

    // Per month: the key is the first day of the month.
    [Fact]
    public async Task GetTotalsByPeriodAsync_GroupsByMonth()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByPeriodAsync(1, null, null, SummaryPeriod.Month, CancellationToken.None);

        Assert.Equal(
            new[] { new PeriodTotalDto(new DateOnly(2026, 10, 1), 3, 85.43m), new PeriodTotalDto(new DateOnly(2026, 11, 1), 1, 30.00m) },
            totals);
    }

    // Per week: the key is the Monday. Sunday 11 Oct belongs to the week that started on Monday 5 Oct.
    [Fact]
    public async Task GetTotalsByPeriodAsync_GroupsByWeek_StartingOnMonday()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByPeriodAsync(1, null, null, SummaryPeriod.Week, CancellationToken.None);

        Assert.Equal(
            new[] { new PeriodTotalDto(new DateOnly(2026, 10, 5), 3, 85.43m), new PeriodTotalDto(new DateOnly(2026, 11, 2), 1, 30.00m) },
            totals);
    }

    // Per day: one line for every day that has expenses, in date order.
    [Fact]
    public async Task GetTotalsByPeriodAsync_GroupsByDay()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByPeriodAsync(1, null, null, SummaryPeriod.Day, CancellationToken.None);

        Assert.Equal(
            new[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 11), new DateOnly(2026, 11, 2) },
            totals.Select(p => p.PeriodStart));
    }

    // Only the expenses between 'from' and 'to' are counted.
    [Fact]
    public async Task GetTotalsByPeriodAsync_UsesTheDateRange()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByPeriodAsync(1, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 31), SummaryPeriod.Month, CancellationToken.None);

        Assert.Equal(new[] { new PeriodTotalDto(new DateOnly(2026, 10, 1), 2, 73.96m) }, totals);
    }

    // A user without expenses gets an empty list.
    [Fact]
    public async Task GetTotalsByPeriodAsync_NoExpenses_ReturnsAnEmptyList()
    {
        var setup = SetupWithExpensesForTotals();

        var totals = await setup.Service.GetTotalsByPeriodAsync(99, null, null, SummaryPeriod.Month, CancellationToken.None);

        Assert.Empty(totals);
    }

    // A 'from' date after the 'to' date is a bad request.
    [Fact]
    public async Task GetTotalsByPeriodAsync_FromAfterTo_ThrowsValidation()
    {
        var setup = SetupWithExpensesForTotals();

        var error = await Assert.ThrowsAsync<AppException>(
            () => setup.Service.GetTotalsByPeriodAsync(1, new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1), SummaryPeriod.Month, CancellationToken.None));

        Assert.Equal(ErrorType.Validation, error.Type);
    }
}
