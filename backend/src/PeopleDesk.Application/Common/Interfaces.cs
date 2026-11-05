namespace PeopleDesk.Application.Common;

/// <summary>Identity of the caller for the current request (parsed from the access token).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    Guid? EmployeeId { get; }
    string? Email { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }
    IReadOnlySet<string> PermissionSet { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }

    bool Has(string permissionCode);
    void Ensure(string permissionCode);
}

/// <summary>
/// Tenant scope for the current request. Set by middleware from the token's organization claim,
/// overridable with X-Tenant-Id for platform admins. EF global query filters bind to this.
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid organizationId);
}

public interface IDateTime
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

/// <summary>Queues an email via the transactional outbox (never sends inline).</summary>
public interface IEmailOutbox
{
    Task EnqueueAsync(Guid? organizationId, string toEmail, string subject, string bodyHtml, string? bodyText = null,
        CancellationToken ct = default);
}

public interface IFileStorage
{
    /// <summary>Validated, org-scoped storage. Returns the storage key.</summary>
    Task<string> SaveAsync(Guid organizationId, string folder, Stream content, string fileExtension, CancellationToken ct = default);
    Task<(Stream Content, string ContentType)?> OpenAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
    /// <summary>True when the hash uses outdated parameters and should be rehashed on next successful login.</summary>
    bool NeedsRehash(string hash);
}

/// <summary>RFC 6238 TOTP for optional two-factor auth.</summary>
public interface ITotpService
{
    string GenerateSecret();
    string OtpAuthUri(string email, string secret, string issuer);
    bool Verify(string secret, string code, TimeSpan? drift = null);
    string ComputeCode(string secret, DateTimeOffset at);
}

public interface ITokenIssuer
{
    (string Token, DateTime ExpiresAtUtc) IssueAccessToken(Guid userId, Guid organizationId, string email,
        Guid? employeeId, bool isPlatformAdmin, IEnumerable<string> permissions);
}

public interface IPayslipPdfGenerator
{
    /// <summary>Renders the payslip PDF for a computed payroll item.</summary>
    byte[] Generate(PayslipModel model);
}

public interface IXlsxExporter
{
    byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<IEnumerable<object?>> rows);
}

/// <summary>Snapshot passed to the PDF generator (decoupled from EF entities).</summary>
public record PayslipModel(
    string OrganizationName,
    string? OrganizationAddress,
    string Currency,
    string PayslipNumber,
    string PeriodName,
    string EmployeeNumber,
    string EmployeeName,
    string? Department,
    string? Position,
    string? BankAccountMasked,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    IReadOnlyList<(string Label, decimal Amount)> Earnings,
    IReadOnlyList<(string Label, decimal Amount)> Deductions);
