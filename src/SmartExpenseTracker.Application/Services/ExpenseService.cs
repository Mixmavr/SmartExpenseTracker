using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Mappers;
using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Services;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICurrencyConverter _currencyConverter;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        ICategoryRepository categoryRepository,
        ICurrencyConverter currencyConverter)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _currencyConverter = currencyConverter;
    }

    public async Task<List<ExpenseDto>> GetAllAsync(int userId, ExpenseFilter filter, CancellationToken cancellationToken)
    {
        if (filter.From > filter.To)
        {
            throw new AppException(ErrorType.Validation, "The 'from' date cannot be after the 'to' date.");
        }

        var currency = string.IsNullOrWhiteSpace(filter.Currency) ? null : filter.Currency.Trim().ToUpperInvariant();

        var expenses = await _expenseRepository.GetAllAsync(userId, filter with { Currency = currency }, cancellationToken);
        return expenses.Select(e => e.ToDto()).ToList();
    }

    public async Task<ExpenseDto> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var expense = await GetOwnedExpenseAsync(userId, id, cancellationToken);
        return expense.ToDto();
    }

    public async Task<ExpenseDto> CreateAsync(int userId, ExpenseInput input, CancellationToken cancellationToken)
    {
        var category = await GetOwnedCategoryAsync(userId, input.CategoryId, cancellationToken);
        var currency = input.Currency.Trim().ToUpperInvariant();

        var conversion = await _currencyConverter.ConvertToEurAsync(input.Amount, currency, cancellationToken);

        var expense = new Expense
        {
            Amount = input.Amount,
            Currency = currency,
            Date = input.Date ?? DateOnly.FromDateTime(DateTime.Now),
            Description = CleanDescription(input.Description),
            ExchangeRate = conversion.ExchangeRate,
            AmountInEur = conversion.AmountInEur,
            UserId = userId,
            CategoryId = category.Id,
            Category = category,
        };

        await _expenseRepository.AddAsync(expense, cancellationToken);

        return expense.ToDto();
    }

    public async Task<ExpenseDto> UpdateAsync(int userId, int id, ExpenseInput input, CancellationToken cancellationToken)
    {
        var expense = await GetOwnedExpenseAsync(userId, id, cancellationToken);
        var category = await GetOwnedCategoryAsync(userId, input.CategoryId, cancellationToken);
        var currency = input.Currency.Trim().ToUpperInvariant();

        // Convert again only if the money changed. Otherwise we keep the rate saved at creation.
        if (expense.Amount != input.Amount || expense.Currency != currency)
        {
            var conversion = await _currencyConverter.ConvertToEurAsync(input.Amount, currency, cancellationToken);
            expense.ExchangeRate = conversion.ExchangeRate;
            expense.AmountInEur = conversion.AmountInEur;
        }

        expense.Amount = input.Amount;
        expense.Currency = currency;
        expense.Date = input.Date ?? expense.Date;
        expense.Description = CleanDescription(input.Description);
        expense.CategoryId = category.Id;
        expense.Category = category;

        await _expenseRepository.UpdateAsync(expense, cancellationToken);

        return expense.ToDto();
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var expense = await GetOwnedExpenseAsync(userId, id, cancellationToken);
        await _expenseRepository.DeleteAsync(expense, cancellationToken);
    }

    private async Task<Expense> GetOwnedExpenseAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(userId, id, cancellationToken);

        if (expense == null)
        {
            throw new AppException(ErrorType.NotFound, $"The expense {id} was not found.");
        }

        return expense;
    }

    private async Task<Category> GetOwnedCategoryAsync(int userId, int categoryId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(userId, categoryId, cancellationToken);

        if (category == null)
        {
            throw new AppException(ErrorType.Validation, $"The category {categoryId} was not found.");
        }

        return category;
    }

    private static string? CleanDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}
