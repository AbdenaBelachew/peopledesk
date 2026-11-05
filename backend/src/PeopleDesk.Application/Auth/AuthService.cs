using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Organizations;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Auth;

public class AuthService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTime clock,
    IPasswordHasher passwordHasher,
    ITotpService totp,
    ITokenIssuer tokenIssuer,
    IEmailOutbox emailOutbox,
    IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _opts = authOptions.Value;

    // ─── Registration ────────────────────────────────────────────────────────────

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await new RegisterRequestValidator().ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, ct))
            throw new ConflictException("An account with this email already exists.");
        if (await db.Organizations.AnyAsync(o => o.Slug == slug, ct))
            throw new ConflictException("This organization slug is already taken.");

        var org = new Organization
        {
            Name = request.OrganizationName.Trim(),
            Slug = slug,
            Email = email,
            Country = request.Country,
            TimeZone = string.Equals(request.Country, "Ethiopia", StringComparison.OrdinalIgnoreCase)
                ? "Africa/Addis_Ababa" : "UTC",
            Currency = string.Equals(request.Country, "Ethiopia", StringComparison.OrdinalIgnoreCase)
                ? "ETB" : "USD"
        };

        var user = new User
        {
            OrganizationId = org.Id,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Organizations.Add(org);
            db.Users.Add(user);

            await OrganizationDefaults.CreateDefaultStructureAsync(db, org, request.Country, ct);
            await OrganizationDefaults.CreateSystemRolesAsync(db, org, user.Id, ct);

            var trialPlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "trial", ct);
            db.Subscriptions.Add(new Subscription
            {
                OrganizationId = org.Id,
                PlanId = trialPlan?.Id ?? Guid.Empty,
                Status = SubscriptionStatus.Trialing,
                StartedAtUtc = clock.UtcNow,
                TrialEndsAtUtc = clock.UtcNow.AddDays(_opts.TrialDays)
            });

            await QueueVerificationEmailAsync(user, ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        await emailOutbox.EnqueueAsync(org.Id, email, EmailTemplates.Welcome(user.FirstName, org.Name).Subject,
            EmailTemplates.Welcome(user.FirstName, org.Name).Html, ct: ct);

        var (access, expires) = tokenIssuer.IssueAccessToken(user.Id, org.Id, user.Email, null, false, Permissions.DefaultRolePermissions[Permissions.Roles.Owner]);
        var refresh = await IssueRefreshTokenAsync(user, ct);

        return new AuthResult
        {
            AccessToken = access,
            AccessTokenExpiresAtUtc = expires,
            RefreshToken = refresh,
            UserId = user.Id,
            OrganizationId = org.Id,
            OrganizationName = org.Name
        };
    }

    // ─── Login / refresh / logout ─────────────────────────────────────────────────

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await new LoginRequestValidator().ValidateAndThrowAsync(request, ct);

        var normalized = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        if (user is null)
            throw new BadRequestException("Invalid email or password.");

        if (user.LockoutEndUtc is { } lockUntil && lockUntil > clock.UtcNow)
            throw new BadRequestException($"Account temporarily locked. Try again after {lockUntil:HH:mm} UTC.");

        if (!user.IsActive)
            throw new BadRequestException("This account has been deactivated. Contact your administrator.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _opts.MaxFailedLogins)
            {
                user.LockoutEndUtc = clock.UtcNow.AddMinutes(_opts.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct);
            throw new BadRequestException("Invalid email or password.");
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
            user.PasswordHash = passwordHasher.Hash(request.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = clock.UtcNow;

        // Optional 2FA: gate token issuance behind a short-lived pending token.
        if (user.TwoFactorEnabled)
        {
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                if (user.TwoFactorSecret is null || !totp.Verify(user.TwoFactorSecret, request.Code!))
                {
                    await db.SaveChangesAsync(ct);
                    throw new BadRequestException("Invalid two-factor code.");
                }
            }
            else
            {
                await db.SaveChangesAsync(ct);
                var pending = NewToken();
                db.AuthTokens.Add(new AuthToken
                {
                    UserId = user.Id,
                    Type = AuthTokenType.TwoFactorPending,
                    TokenHash = Hash(pending),
                    ExpiresAtUtc = clock.UtcNow.AddMinutes(_opts.PendingTokenMinutes)
                });
                await db.SaveChangesAsync(ct);
                return new LoginResult { TwoFactorRequired = true, PendingToken = pending, UserId = user.Id };
            }
        }

        await db.SaveChangesAsync(ct);
        return await IssueSessionAsync(user, ct);
    }

    public async Task<LoginResult> VerifyTwoFactorAsync(TwoFactorVerifyRequest request, CancellationToken ct = default)
    {
        var token = await db.AuthTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == Hash(request.PendingToken) && t.Type == AuthTokenType.TwoFactorPending, ct);
        if (token is null || !token.IsUsable)
            throw new BadRequestException("This verification request has expired. Please sign in again.");

        token.UsedAtUtc = clock.UtcNow;
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw new NotFoundException("User");

        if (user.TwoFactorSecret is null || !totp.Verify(user.TwoFactorSecret, request.Code))
            throw new BadRequestException("Invalid two-factor code.");

        user.LastLoginAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await IssueSessionAsync(user, ct);
    }

    /// <summary>Rotates the refresh token. Replay of a rotated token revokes the whole family (theft detection).</summary>
    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new BadRequestException("Missing refresh token.");

        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            throw new ForbiddenException("Invalid refresh token.");

        if (token.RevokedAtUtc is not null)
        {
            // Reuse of a rotated token: assume theft, revoke everything for this user.
            var all = await db.RefreshTokens.Where(t => t.UserId == token.UserId && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var t in all) t.RevokedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new ForbiddenException("Refresh token reuse detected. All sessions were revoked; sign in again.");
        }

        if (token.ExpiresAtUtc <= clock.UtcNow)
            throw new ForbiddenException("Refresh token expired. Sign in again.");

        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw new NotFoundException("User");
        if (!user.IsActive)
            throw new ForbiddenException("Account deactivated.");

        token.RevokedAtUtc = clock.UtcNow;
        var newRefresh = await IssueRefreshTokenAsync(user, ct);
        token.ReplacedByTokenHash = Hash(newRefresh);

        await db.SaveChangesAsync(ct);

        var (access, expires) = await IssueAccessTokenFor(user, ct);
        return new AuthResult
        {
            AccessToken = access,
            AccessTokenExpiresAtUtc = expires,
            RefreshToken = newRefresh,
            UserId = user.Id,
            OrganizationId = user.OrganizationId,
            OrganizationName = (await db.Organizations.FirstAsync(o => o.Id == user.OrganizationId, ct)).Name
        };
    }

    public async Task LogoutAsync(string? refreshToken, bool revokeAll = false, CancellationToken ct = default)
    {
        if (revokeAll && currentUser.UserId is { } uid)
        {
            var all = await db.RefreshTokens.Where(t => t.UserId == uid && t.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var t in all) t.RevokedAtUtc = clock.UtcNow;
        }
        else if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(refreshToken), ct);
            if (token is { RevokedAtUtc: null }) token.RevokedAtUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    // ─── Password / email flows ──────────────────────────────────────────────────

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        // Always succeed to avoid account enumeration; only queue mail when the user exists.
        if (user is null || !user.IsActive) return;

        var raw = NewToken();
        db.AuthTokens.Add(new AuthToken
        {
            UserId = user.Id,
            Type = AuthTokenType.PasswordReset,
            TokenHash = Hash(raw),
            ExpiresAtUtc = clock.UtcNow.AddHours(_opts.ResetTokenHours)
        });
        await db.SaveChangesAsync(ct);

        var link = $"{_opts.FrontendBaseUrl}/reset-password?token={raw}";
        var (subject, html) = EmailTemplates.PasswordReset(user.FirstName, link);
        await emailOutbox.EnqueueAsync(user.OrganizationId, user.Email, subject, html, ct: ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        await new ResetPasswordRequestValidator().ValidateAndThrowAsync(request, ct);

        var token = await db.AuthTokens.FirstOrDefaultAsync(t =>
            t.TokenHash == Hash(request.Token) && t.Type == AuthTokenType.PasswordReset, ct);
        if (token is null || !token.IsUsable)
            throw new BadRequestException("This reset link is invalid or has expired.");

        token.UsedAtUtc = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw new NotFoundException("User");

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        // Force re-login everywhere.
        var sessions = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var s in sessions) s.RevokedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var t = await db.AuthTokens.FirstOrDefaultAsync(a =>
            a.TokenHash == Hash(token) && a.Type == AuthTokenType.EmailVerification, ct);
        if (t is null || !t.IsUsable)
            throw new BadRequestException("This verification link is invalid or has expired.");

        t.UsedAtUtc = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == t.UserId, ct)
            ?? throw new NotFoundException("User");
        user.EmailVerifiedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResendVerificationEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is null || user.EmailVerifiedAtUtc is not null) return;

        var recent = await db.AuthTokens.CountAsync(a =>
            a.UserId == user.Id && a.Type == AuthTokenType.EmailVerification &&
            a.CreatedAtUtc > clock.UtcNow.AddMinutes(-10), ct);
        if (recent >= 3)
            throw new BadRequestException("Verification email already sent recently. Please wait a few minutes.");

        await QueueVerificationEmailAsync(user, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task QueueVerificationEmailAsync(User user, CancellationToken ct)
    {
        var raw = NewToken();
        db.AuthTokens.Add(new AuthToken
        {
            UserId = user.Id,
            Type = AuthTokenType.EmailVerification,
            TokenHash = Hash(raw),
            ExpiresAtUtc = clock.UtcNow.AddHours(_opts.VerificationTokenHours)
        });
        var link = $"{_opts.FrontendBaseUrl}/verify-email?token={raw}";
        var (subject, html) = EmailTemplates.VerifyEmail(user.FirstName, link);
        await emailOutbox.EnqueueAsync(user.OrganizationId, user.Email, subject, html, ct: ct);
    }

    // ─── 2FA management ──────────────────────────────────────────────────────────

    public async Task<SetupTwoFactorResponse> SetupTwoFactorAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        user.TwoFactorSecret ??= totp.GenerateSecret();
        await db.SaveChangesAsync(ct);

        return new SetupTwoFactorResponse
        {
            Secret = user.TwoFactorSecret,
            OtpAuthUri = totp.OtpAuthUri(user.Email, user.TwoFactorSecret, "PeopleDesk")
        };
    }

    public async Task EnableTwoFactorAsync(string code, CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user.TwoFactorSecret is null)
            throw new BadRequestException("Run two-factor setup first.");
        if (!totp.Verify(user.TwoFactorSecret, code))
            throw new BadRequestException("Invalid code. Check your authenticator and try again.");
        user.TwoFactorEnabled = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task DisableTwoFactorAsync(string password, CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        if (!passwordHasher.Verify(password, user.PasswordHash))
            throw new BadRequestException("Incorrect password.");
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await db.SaveChangesAsync(ct);
    }

    // ─── Profile ─────────────────────────────────────────────────────────────────

    public async Task<MeResponse> GetMeAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } uid)
            throw new ForbiddenException("Not authenticated.");

        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == uid, ct) ?? throw new NotFoundException("User");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw new NotFoundException("Organization");

        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.OrganizationId == org.Id, ct);

        var perms = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code)).ToHashSet();
        var isPlatformAdmin = org.Type == OrganizationType.Platform && perms.Contains(Permissions.PlatformManage);

        return new MeResponse
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmployeeId = user.EmployeeId,
            IsPlatformAdmin = isPlatformAdmin,
            EmailVerified = user.EmailVerifiedAtUtc is not null,
            TwoFactorEnabled = user.TwoFactorEnabled,
            Permissions = [.. perms],
            Roles = [.. user.UserRoles.Select(ur => ur.Role.Name).Distinct()],
            OrganizationName = org.Name,
            OrganizationSlug = org.Slug,
            LogoUrl = org.LogoUrl,
            BrandColor = org.BrandColor,
            TimeZone = org.TimeZone,
            Currency = org.Currency,
            Subscription = sub is null ? null : new SubscriptionInfo
            {
                PlanName = sub.Plan.Name,
                PlanCode = sub.Plan.Code,
                MaxEmployees = sub.Plan.MaxEmployees,
                Status = sub.Status.ToString(),
                TrialEndsAtUtc = sub.TrialEndsAtUtc,
                CurrentPeriodEndUtc = sub.CurrentPeriodEndUtc
            }
        };
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<User> GetCurrentUserAsync(CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw new ForbiddenException("Not authenticated.");

    private async Task<(string Token, DateTime Expires)> IssueAccessTokenFor(User user, CancellationToken ct)
    {
        // Resolve permission codes via a direct query (robust regardless of navigation includes).
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        var perms = (await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync(ct)).ToHashSet();

        var orgType = await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.Type).FirstAsync(ct);
        var isPlatformAdmin = orgType == OrganizationType.Platform && perms.Contains(Permissions.PlatformManage);
        return tokenIssuer.IssueAccessToken(user.Id, user.OrganizationId, user.Email, user.EmployeeId, isPlatformAdmin, perms);
    }

    private async Task<LoginResult> IssueSessionAsync(User user, CancellationToken ct)
    {
        var (access, expires) = await IssueAccessTokenFor(user, ct);
        var refresh = await IssueRefreshTokenAsync(user, ct);
        var orgName = await db.Organizations.Where(o => o.Id == user.OrganizationId).Select(o => o.Name).FirstAsync(ct);

        return new LoginResult
        {
            AccessToken = access,
            AccessTokenExpiresAtUtc = expires,
            RefreshToken = refresh,
            UserId = user.Id,
            OrganizationId = user.OrganizationId,
            OrganizationName = orgName
        };
    }

    private async Task<string> IssueRefreshTokenAsync(User user, CancellationToken ct, string? replacedByHash = null)
    {
        var raw = NewToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAtUtc = clock.UtcNow.AddDays(_opts.RefreshTokenDays),
            CreatedByIp = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent
        });
        await db.SaveChangesAsync(ct);
        return raw;
    }

    private static string NewToken() => Guid.CreateVersion7().ToString("N") + Guid.CreateVersion7().ToString("N");

    private static string Hash(string raw)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
