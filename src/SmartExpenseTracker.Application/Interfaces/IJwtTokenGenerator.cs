using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string Generate(User user);
}
