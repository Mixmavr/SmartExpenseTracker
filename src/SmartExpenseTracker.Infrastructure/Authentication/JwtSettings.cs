namespace SmartExpenseTracker.Infrastructure.Authentication;

public class JwtSettings
{
    // The secret used to sign tokens. Must be at least 32 characters for HS256.
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
