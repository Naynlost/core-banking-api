using System.Security.Claims;

namespace Banking.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // JwtBearer maps the "sub" claim to NameIdentifier by default.
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request without a subject claim.");
}
