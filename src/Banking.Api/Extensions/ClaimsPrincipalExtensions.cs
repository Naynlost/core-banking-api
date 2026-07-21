using System.Security.Claims;

namespace Banking.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // JwtBearer "sub" claim'ini varsayılan olarak NameIdentifier'a eşler
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request without a subject claim.");
}
