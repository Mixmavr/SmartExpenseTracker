using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<List<CategoryDto>> GetAllAsync(int userId, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllAsync(userId, cancellationToken);
        return categories.Select(ToDto).ToList();
    }

    public async Task<CategoryDto> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var category = await GetOwnedCategoryAsync(userId, id, cancellationToken);
        return ToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(int userId, string name, CancellationToken cancellationToken)
    {
        name = name.Trim();

        if (await _categoryRepository.NameExistsAsync(userId, name, null, cancellationToken))
        {
            throw new AppException(ErrorType.Conflict, $"You already have a category named '{name}'.");
        }

        var category = new Category { Name = name, UserId = userId };
        await _categoryRepository.AddAsync(category, cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int userId, int id, string name, CancellationToken cancellationToken)
    {
        var category = await GetOwnedCategoryAsync(userId, id, cancellationToken);
        name = name.Trim();

        if (await _categoryRepository.NameExistsAsync(userId, name, id, cancellationToken))
        {
            throw new AppException(ErrorType.Conflict, $"You already have a category named '{name}'.");
        }

        category.Name = name;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        return ToDto(category);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var category = await GetOwnedCategoryAsync(userId, id, cancellationToken);

        if (await _categoryRepository.HasExpensesAsync(id, cancellationToken))
        {
            throw new AppException(ErrorType.Conflict, "The category has expenses and cannot be deleted.");
        }

        await _categoryRepository.DeleteAsync(category, cancellationToken);
    }

    private async Task<Category> GetOwnedCategoryAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(userId, id, cancellationToken);

        if (category == null)
        {
            throw new AppException(ErrorType.NotFound, $"The category {id} was not found.");
        }

        return category;
    }

    //mapper
    private static CategoryDto ToDto(Category category)
    {
        return new CategoryDto(category.Id, category.Name);
    }
}
