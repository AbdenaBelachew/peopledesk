using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Notifications;
using PeopleDesk.Application.Auth;
using PeopleDesk.Application.Payroll;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Application.Payslips;

public class PayslipService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    IPayslipPdfGenerator pdfGenerator,
    IFileStorage storage,
    IEmailOutbox emailOutbox,
    NotificationService notifications,
    Microsoft.Extensions.Options.IOptions<AuthOptions> authOptions)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    public record PayslipDto
    {
        public Guid Id { get; init; }
        public Guid EmployeeId { get; init; }
        public string EmployeeName { get; init; } = default!;
        public string EmployeeNumber { get; init; } = default!;
        public string PayslipNumber { get; init; } = default!;
        public string PeriodName { get; init; } = default!;
        public string Currency { get; init; } = default!;
        public decimal GrossPay { get; init; }
        public decimal TotalDeductions { get; init; }
        public decimal NetPay { get; init; }
        public DateTime IssuedAtUtc { get; init; }
        public DateTime? PublishedAtUtc { get; init; }
    }

    /// <summary>Generates and archives PDF payslips for a finalized run and notifies employees. Idempotent per run.</summary>
    public async Task<int> GenerateForRunAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollFinalize);

        var run = await db.PayrollRuns.Include(r => r.Period).AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status != PayrollRunStatus.Finalized)
            throw new BusinessRuleException("Payslips can only be generated for finalized runs.");

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == OrgId, ct);
        var existing = await db.Payslips.AsNoTracking().Where(p => p.PayrollRunId == runId).CountAsync(ct);
        if (existing > 0) return 0; // already generated

        var items = await db.PayrollItems.AsNoTracking().Where(i => i.PayrollRunId == runId).ToListAsync(ct);
        var employeeIds = items.Select(i => i.EmployeeId).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);
        var departments = await db.Departments.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var positions = await db.Positions.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        var count = 0;
        var sequence = await db.Payslips.AsNoTracking().CountAsync(p => p.OrganizationId == OrgId, ct);

        foreach (var item in items)
        {
            var employee = employees.GetValueOrDefault(item.EmployeeId);
            if (employee is null) continue;
            sequence++;

            var earnings = await db.PayrollEarnings.AsNoTracking()
                .Where(e => e.PayrollItemId == item.Id).ToListAsync(ct);
            var deductions = await db.PayrollDeductions.AsNoTracking()
                .Where(d => d.PayrollItemId == item.Id).ToListAsync(ct);

            var payslipNumber = $"PS-{run.Period.Name}-{employee.EmployeeNumber}";
            var model = new PayslipModel(
                OrganizationName: org.Name,
                OrganizationAddress: string.Join(", ", new[] { org.AddressLine, org.City, org.Country }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Currency: item.Currency,
                PayslipNumber: payslipNumber,
                PeriodName: run.Period.Name,
                EmployeeNumber: employee.EmployeeNumber,
                EmployeeName: $"{employee.FirstName} {employee.LastName}",
                Department: employee.DepartmentId is { } dep && departments.TryGetValue(dep, out var dn) ? dn : null,
                Position: employee.PositionId is { } pos && positions.TryGetValue(pos, out var pt) ? pt : null,
                BankAccountMasked: employee.BankAccount is { Length: > 4 } bank ? "•••• " + bank[^4..] : null,
                GrossPay: item.GrossSalary,
                TotalDeductions: item.TotalDeductions,
                NetPay: item.NetSalary,
                Earnings: earnings.Select(e => (e.Label, e.Amount)).ToList(),
                Deductions: deductions.Select(d => (d.Label, d.Amount)).ToList());

            var pdf = pdfGenerator.Generate(model);
            await using var stream = new MemoryStream(pdf);
            var key = await storage.SaveAsync(OrgId, "payslips", stream, ".pdf", ct);

            db.Payslips.Add(new Payslip
            {
                OrganizationId = OrgId,
                PayrollRunId = runId,
                PayrollPeriodId = run.PayrollPeriodId,
                EmployeeId = item.EmployeeId,
                PayrollItemId = item.Id,
                PayslipNumber = payslipNumber,
                PeriodName = run.Period.Name,
                Currency = item.Currency,
                GrossPay = item.GrossSalary,
                TotalDeductions = item.TotalDeductions,
                NetPay = item.NetSalary,
                IssuedAtUtc = clock.UtcNow,
                StorageKey = key,
                FileSizeBytes = pdf.LongLength
            });
            count++;
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>Marks payslips published: notifies employees in-app + email. Payslips are downloadable regardless of publish state by authorized users.</summary>
    public async Task<int> PublishAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayslipsPublish);

        var payslips = await db.Payslips
            .Where(p => p.PayrollRunId == runId && p.PublishedAtUtc == null).ToListAsync(ct);
        if (payslips.Count == 0) return 0;

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == OrgId, ct);
        var baseUrl = authOptions.Value.FrontendBaseUrl;
        var now = clock.UtcNow;

        foreach (var p in payslips)
        {
            p.PublishedAtUtc = now;
            p.PublishedBy = user.Email;

            var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == p.EmployeeId, ct);
            if (employee?.UserId is { } userId)
            {
                await notifications.NotifyUserAsync(userId, OrgId, NotificationType.PayslipPublished,
                    "Payslip available",
                    $"Your payslip for {p.PeriodName} is now available (net {p.NetPay:0.00} {p.Currency}).",
                    "/payslips", ct);

                var link = $"{baseUrl}/payslips";
                var (subject, html) = EmailTemplates.PayslipReady(employee.FirstName, p.PeriodName, link);
                var emailTo = employee.WorkEmail ?? employee.PersonalEmail ?? (employee.UserId is not null ? (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == employee.UserId, ct))?.Email : null);
            if (emailTo is not null)
                await emailOutbox.EnqueueAsync(OrgId, emailTo, subject, html, ct: ct);
            }
        }

        await db.SaveChangesAsync(ct);
        return payslips.Count;
    }

    public async Task<PagedResult<PayslipDto>> ListAsync(PagedRequest page, Guid? employeeId, Guid? periodId, CancellationToken ct = default)
    {
        Guid? effectiveEmployeeId = employeeId;
        if (!user.Has(Permissions.PayslipsViewAll))
            effectiveEmployeeId = user.EmployeeId
                ?? throw new ForbiddenException("Your account is not linked to an employee record.");

        var query = db.Payslips.AsNoTracking();
        if (effectiveEmployeeId is { } emp) query = query.Where(p => p.EmployeeId == emp);
        if (periodId is { } pid) query = query.Where(p => p.PayrollPeriodId == pid);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.IssuedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100)).ToListAsync(ct);

        var employeeIds = rows.Select(p => p.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        return new PagedResult<PayslipDto>(
            rows.Select(p =>
            {
                var e = employees.GetValueOrDefault(p.EmployeeId);
                return new PayslipDto
                {
                    Id = p.Id,
                    EmployeeId = p.EmployeeId,
                    EmployeeName = e is null ? "?" : $"{e.FirstName} {e.LastName}",
                    EmployeeNumber = e?.EmployeeNumber ?? "?",
                    PayslipNumber = p.PayslipNumber,
                    PeriodName = p.PeriodName,
                    Currency = p.Currency,
                    GrossPay = p.GrossPay,
                    TotalDeductions = p.TotalDeductions,
                    NetPay = p.NetPay,
                    IssuedAtUtc = p.IssuedAtUtc,
                    PublishedAtUtc = p.PublishedAtUtc
                };
            }).ToList(), page.Page, page.PageSize, total);
    }

    /// <summary>Downloads a payslip PDF. Employees may only access their own unless they hold PayslipsViewAll.</summary>
    public async Task<(byte[] Pdf, Payslip Payslip)> DownloadAsync(Guid payslipId, CancellationToken ct = default)
    {
        var payslip = await db.Payslips.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payslipId, ct)
            ?? throw new NotFoundException("Payslip", payslipId);

        if (payslip.EmployeeId != user.EmployeeId && !user.Has(Permissions.PayslipsViewAll))
            throw new ForbiddenException("You can only download your own payslips.");

        var opened = await storage.OpenAsync(payslip.StorageKey, ct)
            ?? throw new NotFoundException("Payslip file (missing from storage)");
        await using var stream = opened.Content;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return (ms.ToArray(), payslip);
    }
}
