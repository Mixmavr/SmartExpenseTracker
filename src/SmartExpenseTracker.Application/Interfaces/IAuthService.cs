using SmartExpenseTracker.Application.Models;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(string email, string password, CancellationToken cancellationToken);

    Task<TokenDto> LoginAsync(string email, string password, CancellationToken cancellationToken);
}
