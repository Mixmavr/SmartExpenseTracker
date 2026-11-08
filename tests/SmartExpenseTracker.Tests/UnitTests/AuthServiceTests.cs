using Microsoft.AspNetCore.Identity;
using SmartExpenseTracker.Application.Exceptions;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Services;
using SmartExpenseTracker.Domain.Entities;

namespace SmartExpenseTracker.Tests.UnitTests;

public class AuthServiceTests
{
    // A fake "database of users" that lives in a list.
    private class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.Any(u => u.Email == email));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Email == email));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            user.Id = Users.Count + 1;
            Users.Add(user);
            return Task.CompletedTask;
        }
    }

    // A fake token generator, so these tests do not depend on JWT details.
    private class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public string Generate(User user) => $"token-for-{user.Id}";
    }

    // Uses the real password hasher: that is what we want to test with.
    private static AuthService CreateService(FakeUserRepository repository)
    {
        return new AuthService(repository, new PasswordHasher<User>(), new FakeJwtTokenGenerator());
    }

    // The password is saved as a hash, never as plain text, and the hash can be verified later.
    [Fact]
    public async Task RegisterAsync_SavesAHashAndNotThePassword()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        var saved = repository.Users.Single();
        Assert.NotEqual("Secret123!", saved.PasswordHash);
        Assert.DoesNotContain("Secret123!", saved.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(saved, saved.PasswordHash, "Secret123!"));
    }

    // The email is saved in lower case and without spaces.
    [Fact]
    public async Task RegisterAsync_NormalizesTheEmail()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var user = await service.RegisterAsync("  Jane@Example.COM ", "Secret123!", CancellationToken.None);

        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("jane@example.com", repository.Users.Single().Email);
    }

    // We get back the new user's id and email (and not the hash).
    [Fact]
    public async Task RegisterAsync_ReturnsTheNewUser()
    {
        var service = CreateService(new FakeUserRepository());

        var user = await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        Assert.Equal(1, user.Id);
        Assert.Equal("jane@example.com", user.Email);
    }

    // The same email cannot be registered twice, even if it is written with different letters.
    [Fact]
    public async Task RegisterAsync_EmailAlreadyRegistered_Throws()
    {
        var service = CreateService(new FakeUserRepository());
        await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.RegisterAsync("JANE@example.com", "Another123!", CancellationToken.None));

        Assert.Equal(ErrorType.Conflict, error.Type);
    }

    // With the right email and password we get a token (the email can be written with other letters).
    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsAToken()
    {
        var service = CreateService(new FakeUserRepository());
        await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        var result = await service.LoginAsync(" Jane@Example.com ", "Secret123!", CancellationToken.None);

        Assert.Equal("token-for-1", result.Token);
    }

    // A wrong password is rejected with Unauthorized.
    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        var service = CreateService(new FakeUserRepository());
        await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.LoginAsync("jane@example.com", "WrongPassword1", CancellationToken.None));

        Assert.Equal(ErrorType.Unauthorized, error.Type);
    }

    // An unknown email gives the exact same error as a wrong password, so nobody can find out which emails exist.
    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsTheSameErrorAsWrongPassword()
    {
        var service = CreateService(new FakeUserRepository());
        await service.RegisterAsync("jane@example.com", "Secret123!", CancellationToken.None);

        var unknownEmail = await Assert.ThrowsAsync<AppException>(
            () => service.LoginAsync("nobody@example.com", "Secret123!", CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<AppException>(
            () => service.LoginAsync("jane@example.com", "WrongPassword1", CancellationToken.None));

        Assert.Equal(ErrorType.Unauthorized, unknownEmail.Type);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }
}
