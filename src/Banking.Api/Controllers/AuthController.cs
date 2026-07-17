using Banking.Api.Contracts;
using Banking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Banking.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenGenerator tokenGenerator,
    IRefreshTokenService refreshTokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return Created();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Same response whether the email or the password is wrong.
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Problem(
                title: "Invalid credentials.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(await CreateAuthResponseAsync(user, cancellationToken));
    }

    /// <summary>
    /// Exchanges a refresh token for a fresh token pair. The presented token is
    /// consumed (rotation); reusing a consumed token revokes all of the user's
    /// refresh tokens.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var rotation = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);

        // Unknown, expired and reused tokens all get the same answer.
        if (rotation is null)
        {
            return Problem(
                title: "Invalid refresh token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = tokenGenerator.CreateToken(rotation.User);
        return Ok(new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            rotation.NewToken.Token,
            rotation.NewToken.ExpiresAtUtc));
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = tokenGenerator.CreateToken(user);
        var refresh = await refreshTokens.IssueAsync(user, cancellationToken);
        return new AuthResponse(token.AccessToken, token.ExpiresAtUtc, refresh.Token, refresh.ExpiresAtUtc);
    }
}
