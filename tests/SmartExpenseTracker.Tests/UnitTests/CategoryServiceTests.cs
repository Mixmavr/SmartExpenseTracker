using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Services;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Tests.UnitTests;

public class CategoryServiceTests
{
    // A fake "database of categories" that lives in a list.
    private class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = new();

        // Ids of categories that "have expenses" (we have no expenses yet).
        public HashSet<int> CategoriesWithExpenses { get; } = new();

        public Task<List<Category>> GetAllAsync(int userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.Where(c => c.UserId == userId).OrderBy(c => c.Name).ToList());
        }

        public Task<Category?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.FirstOrDefault(c => c.Id == id && c.UserId == userId));
        }

        public Task<bool> NameExistsAsync(int userId, string name, int? excludeId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.Any(c =>
                c.UserId == userId && c.Id != excludeId && c.Name.ToLower() == name.ToLower()));
        }

        public Task<bool> HasExpensesAsync(int categoryId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CategoriesWithExpenses.Contains(categoryId));
        }

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            category.Id = Categories.Count + 1;
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Category category, CancellationToken cancellationToken)
        {
            Categories.Remove(category);
            return Task.CompletedTask;
        }
    }

    // A new category is saved for the user, with the name trimmed.
    [Fact]
    public async Task CreateAsync_SavesTheCategoryForTheUser()
    {
        var repository = new FakeCategoryRepository();
        var service = new CategoryService(repository);

        var category = await service.CreateAsync(1, "  Food ", CancellationToken.None);

        Assert.Equal("Food", category.Name);
        Assert.Equal(1, repository.Categories.Single().UserId);
    }

    // The same name cannot be used twice by the same user, even with different letters.
    [Fact]
    public async Task CreateAsync_DuplicateNameForTheSameUser_Throws()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        await service.CreateAsync(1, "Food", CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.CreateAsync(1, "FOOD", CancellationToken.None));

        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    // Two different users can both have a category with the same name.
    [Fact]
    public async Task CreateAsync_SameNameForAnotherUser_IsAllowed()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        await service.CreateAsync(1, "Food", CancellationToken.None);

        var category = await service.CreateAsync(2, "Food", CancellationToken.None);

        Assert.Equal("Food", category.Name);
    }

    // The list contains only the categories of the user who asks.
    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTheCategoriesOfTheUser()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        await service.CreateAsync(1, "Food", CancellationToken.None);
        await service.CreateAsync(2, "Travel", CancellationToken.None);

        var categories = await service.GetAllAsync(1, CancellationToken.None);

        Assert.Equal("Food", categories.Single().Name);
    }

    // A category of another user looks like it does not exist: NotFound.
    [Fact]
    public async Task GetByIdAsync_CategoryOfAnotherUser_ThrowsNotFound()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        var category = await service.CreateAsync(1, "Food", CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.GetByIdAsync(2, category.Id, CancellationToken.None));

        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    // A category can be renamed.
    [Fact]
    public async Task UpdateAsync_RenamesTheCategory()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        var category = await service.CreateAsync(1, "Food", CancellationToken.None);

        var updated = await service.UpdateAsync(1, category.Id, "Groceries", CancellationToken.None);

        Assert.Equal("Groceries", updated.Name);
    }

    // Saving a category with its own name is not a duplicate; using the name of another category is.
    [Fact]
    public async Task UpdateAsync_OwnNameIsFine_NameOfAnotherCategoryThrows()
    {
        var service = new CategoryService(new FakeCategoryRepository());
        var food = await service.CreateAsync(1, "Food", CancellationToken.None);
        await service.CreateAsync(1, "Travel", CancellationToken.None);

        var same = await service.UpdateAsync(1, food.Id, "Food", CancellationToken.None);
        var error = await Assert.ThrowsAsync<AppException>(
            () => service.UpdateAsync(1, food.Id, "Travel", CancellationToken.None));

        Assert.Equal("Food", same.Name);
        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    // A category without expenses can be deleted.
    [Fact]
    public async Task DeleteAsync_RemovesTheCategory()
    {
        var repository = new FakeCategoryRepository();
        var service = new CategoryService(repository);
        var category = await service.CreateAsync(1, "Food", CancellationToken.None);

        await service.DeleteAsync(1, category.Id, CancellationToken.None);

        Assert.Empty(repository.Categories);
    }

    // A category that has expenses cannot be deleted: Conflict, and it stays.
    [Fact]
    public async Task DeleteAsync_CategoryWithExpenses_ThrowsConflict()
    {
        var repository = new FakeCategoryRepository();
        var service = new CategoryService(repository);
        var category = await service.CreateAsync(1, "Food", CancellationToken.None);
        repository.CategoriesWithExpenses.Add(category.Id);

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.DeleteAsync(1, category.Id, CancellationToken.None));

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Single(repository.Categories);
    }

    // Deleting a category that does not exist gives NotFound.
    [Fact]
    public async Task DeleteAsync_UnknownCategory_ThrowsNotFound()
    {
        var service = new CategoryService(new FakeCategoryRepository());

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.DeleteAsync(1, 99, CancellationToken.None));

        Assert.Equal(ErrorType.NotFound, error.Type);
    }
}
