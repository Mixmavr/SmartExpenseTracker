using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartExpenseTracker.Domain.Entities;
using SmartExpenseTracker.Infrastructure.Authentication;

namespace SmartExpenseTracker.Tests.UnitTests;

public class JwtTokenGeneratorTests
{
    private const string Key = "test-only-secret-key-0123456789-abcdefghij";

    private static JwtTokenGenerator CreateGenerator(string key = Key)
    {
        var settings = new JwtSettings { Key = key, Issuer = "TestIssuer", Audience = "TestAudience", ExpiryMinutes = 30 };
        return new JwtTokenGenerator(Options.Create(settings));
    }

    // The token carries the user id (sub), the email, the issuer and an expiry in the future.
    [Fact]
    public void Generate_PutsTheUserDetailsAndExpiryInTheToken()
    {
        var user = new User { Id = 7, Email = "jane@example.com" };

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateGenerator().Generate(user));

        Assert.Equal("7", token.Subject);
        Assert.Equal("jane@example.com", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("TestIssuer", token.Issuer);
        Assert.True(token.ValidTo > DateTime.UtcNow.AddMinutes(25));
    }

    // The signature only validates with the same secret key: a different key is rejected.
    [Fact]
    public void Generate_TokenIsOnlyValidWithTheSameKey()
    {
        var jwt = CreateGenerator().Generate(new User { Id = 1, Email = "jane@example.com" });

        TokenValidationParameters ParametersWith(string key) => new()
        {
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestAudience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        };

        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(jwt, ParametersWith(Key), out _);

        Assert.ThrowsAny<SecurityTokenException>(
            () => handler.ValidateToken(jwt, ParametersWith("another-secret-key-0123456789-abcdefghij"), out _));
    }
}
