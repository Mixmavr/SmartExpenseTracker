using System.Security.Claims;

namespace SmartExpenseTracker.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // The id of the logged-in user, taken from the "sub" claim of the JWT.
    public static int GetUserId(this ClaimsPrincipal user)
    {
        return int.Parse(user.FindFirstValue("sub")!);
    }
}
