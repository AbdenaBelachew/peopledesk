namespace PeopleDesk.Application.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Base URL of the SPA, used to compose email links.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public int PendingTokenMinutes { get; set; } = 5;
    public int VerificationTokenHours { get; set; } = 48;
    public int ResetTokenHours { get; set; } = 1;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int TrialDays { get; set; } = 14;
}

public record TokenResponse
{
    public string AccessToken { get; init; } = default!;
    public DateTime AccessTokenExpiresAtUtc { get; init; }
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
}

public record RegisterRequest
{
    public string OrganizationName { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string Country { get; init; } = "Ethiopia";
}

public record AuthResult
{
    public string AccessToken { get; init; } = default!;
    public DateTime AccessTokenExpiresAtUtc { get; init; }
    public string RefreshToken { get; init; } = default!;   // controller sets as httpOnly cookie
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = default!;
}

public record LoginRequest
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string? Code { get; init; } = null;
}

public record LoginResult
{
    public bool TwoFactorRequired { get; init; }
    public string? PendingToken { get; init; }
    public string? AccessToken { get; init; }
    public DateTime? AccessTokenExpiresAtUtc { get; init; }
    public string? RefreshToken { get; init; }
    public Guid? UserId { get; init; }
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
}

public record ForgotPasswordRequest { public string Email { get; init; } = default!; }
public record ResetPasswordRequest { public string Token { get; init; } = default!; public string NewPassword { get; init; } = default!; }
public record VerifyEmailRequest { public string Token { get; init; } = default!; }
public record ResendVerificationRequest { public string Email { get; init; } = default!; }
public record TwoFactorVerifyRequest { public string PendingToken { get; init; } = default!; public string Code { get; init; } = default!; }
public record EnableTwoFactorRequest { public string Code { get; init; } = default!; }
public record DisableTwoFactorRequest { public string Password { get; init; } = default!; }
public record SetupTwoFactorResponse { public string Secret { get; init; } = default!; public string OtpAuthUri { get; init; } = default!; }

public record SubscriptionInfo
{
    public string PlanName { get; init; } = default!;
    public string PlanCode { get; init; } = default!;
    public int MaxEmployees { get; init; }
    public string Status { get; init; } = default!;
    public DateTime? TrialEndsAtUtc { get; init; }
    public DateTime? CurrentPeriodEndUtc { get; init; }
}

public record MeResponse
{
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
    public string Email { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public Guid? EmployeeId { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public bool EmailVerified { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyList<string> Roles { get; init; } = [];

    public string OrganizationName { get; init; } = default!;
    public string OrganizationSlug { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string? BrandColor { get; init; }
    public string TimeZone { get; init; } = default!;
    public string Currency { get; init; } = default!;
    public SubscriptionInfo? Subscription { get; init; }
}
