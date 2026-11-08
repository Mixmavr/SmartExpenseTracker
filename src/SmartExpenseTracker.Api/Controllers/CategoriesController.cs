using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseTracker.Api.Extensions;
using SmartExpenseTracker.Api.Models;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>Returns the categories of the logged-in user.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(User.GetUserId(), cancellationToken);
        return Ok(categories);
    }

    /// <summary>Returns one category.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(User.GetUserId(), id, cancellationToken);
        return Ok(category);
    }

    /// <summary>Creates a category.</summary>
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.CreateAsync(User.GetUserId(), request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    /// <summary>Renames a category.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.UpdateAsync(User.GetUserId(), id, request.Name, cancellationToken);
        return Ok(category);
    }

    /// <summary>Deletes a category that has no expenses.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
