using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PeopleDesk.Application.Auth;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(AuthService auth, IOptions<AuthOptions> authOptions) : ControllerBase
{
    public const string RefreshCookieName = "pd_rt";

    private void SetRefreshCookie(string token)
    {
        var days = authOptions.Value.RefreshTokenDays;
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,   // forwarded TLS handled by UseForwardedHeaders
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            MaxAge = TimeSpan.FromDays(days)
        });
    }

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/v1/auth" });

    private string GetRefreshToken() => Request.Cookies[RefreshCookieName] ?? "";

    /// <summary>Register a new organization with its owner account.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        SetRefreshCookie(result.RefreshToken);
        return Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.UserId, result.OrganizationId, result.OrganizationName });
    }

    /// <summary>Login with email/password (and TOTP code when 2FA is enabled).</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        if (result.TwoFactorRequired) return Ok(result);

        SetRefreshCookie(result.RefreshToken!);
        return Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.UserId, result.OrganizationId, result.OrganizationName });
    }

    /// <summary>Second login step when two-factor auth is enabled.</summary>
    [HttpPost("verify-2fa")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> VerifyTwoFactor(TwoFactorVerifyRequest request, CancellationToken ct)
    {
        var result = await auth.VerifyTwoFactorAsync(request, ct);
        SetRefreshCookie(result.RefreshToken!);
        return Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.UserId, result.OrganizationId, result.OrganizationName });
    }

    /// <summary>Rotate the refresh token (httpOnly cookie) and mint a fresh access token.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<ActionResult> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(GetRefreshToken(), ct);
        SetRefreshCookie(result.RefreshToken);
        return Ok(new { result.AccessToken, result.AccessTokenExpiresAtUtc, result.UserId, result.OrganizationId, result.OrganizationName });
    }

    /// <summary>Revoke the current refresh token (pass ?all=true to sign out everywhere).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromQuery] bool all = false, CancellationToken ct = default)
    {
        await auth.LogoutAsync(GetRefreshToken(), all, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return NoContent(); // no enumeration signal
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await auth.VerifyEmailAsync(request.Token, ct);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        await auth.ResendVerificationEmailAsync(request.Email, ct);
        return NoContent();
    }

    /// <summary>Starts TOTP enrollment: returns the secret + otpauth:// URI for QR rendering.</summary>
    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<SetupTwoFactorResponse>> SetupTwoFactor(CancellationToken ct) =>
        Ok(await auth.SetupTwoFactorAsync(ct));

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor(EnableTwoFactorRequest request, CancellationToken ct)
    {
        await auth.EnableTwoFactorAsync(request.Code, ct);
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await auth.DisableTwoFactorAsync(request.Password, ct);
        return NoContent();
    }

    /// <summary>Current user profile, permissions, roles, organization and subscription.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct) => Ok(await auth.GetMeAsync(ct));
}
