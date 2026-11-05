using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Application.Notifications;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.SelfService;

namespace PeopleDesk.Application.SelfService;

public class SelfServiceService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    NotificationService notifications)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    private Guid RequireEmployeeId() => user.EmployeeId
        ?? throw new ForbiddenException("Your account is not linked to an employee record.");

    // ─── Profile change requests ─────────────────────────────────────────────────

    public async Task<ProfileChangeRequestDto> SubmitProfileChangeAsync(SubmitProfileChangeRequest request, CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();

        var changes = request.Changes
            .Where(kv => SelfEditableEmployeeFields.Permitted.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        if (changes.Count == 0)
            throw new BadRequestException("No editable fields supplied. Permitted: " +
                string.Join(", ", SelfEditableEmployeeFields.Permitted));

        var pending = await db.ProfileChangeRequests.AnyAsync(p =>
            p.EmployeeId == employeeId && p.Status == ReviewStatus.Pending, ct);
        if (pending)
            throw new ConflictException("You already have a pending profile change request.");

        var orgSettings = await db.OrganizationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == OrgId, ct);
        var requiresApproval = orgSettings?.RequireApprovalForSensitiveProfileChanges ?? true;

        if (requiresApproval)
        {
            var request1 = new ProfileChangeRequest
            {
                OrganizationId = OrgId,
                EmployeeId = employeeId,
                FieldChangesJson = JsonSerializer.Serialize(changes)
            };
            db.ProfileChangeRequests.Add(request1);
            await db.SaveChangesAsync(ct);

            await notifications.NotifyApproversAsync(NotificationType.PendingApprovals,
                "Profile change request",
                "A new profile change request is awaiting review.",
                "/settings/profile-changes", ct);

            return await ToProfileChangeDtoAsync(request1, ct);
        }

        // Direct apply when approval is disabled.
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId);
        foreach (var (key, value) in changes)
        {
            switch (key.ToLowerInvariant())
            {
                case "phone": employee.Phone = value; break;
                case "personalemail": employee.PersonalEmail = value; break;
                case "addressline": employee.AddressLine = value; break;
                case "city": employee.City = value; break;
            }
        }
        await db.SaveChangesAsync(ct);

        var applied = new ProfileChangeRequest
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            FieldChangesJson = JsonSerializer.Serialize(changes),
            Status = ReviewStatus.Approved,
            ReviewedBy = "auto-approved",
            ReviewedAtUtc = clock.UtcNow,
            ReviewNote = "Approval not required by organization settings"
        };
        db.ProfileChangeRequests.Add(applied);
        await db.SaveChangesAsync(ct);
        return await ToProfileChangeDtoAsync(applied, ct);
    }

    public async Task<ProfileChangeRequestDto> ReviewProfileChangeAsync(Guid requestId, ReviewCorrectionRequestLite request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.HrRequestsHandle);

        var change = await db.ProfileChangeRequests.FirstOrDefaultAsync(p => p.Id == requestId, ct)
            ?? throw new NotFoundException("Profile change request", requestId);
        if (change.Status != ReviewStatus.Pending)
            throw new BusinessRuleException("This request was already reviewed.");

        change.Status = request.Approve ? ReviewStatus.Approved : ReviewStatus.Rejected;
        change.ReviewedBy = user.Email;
        change.ReviewedAtUtc = clock.UtcNow;
        change.ReviewNote = request.ReviewNote;

        if (request.Approve)
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == change.EmployeeId, ct)
                ?? throw new NotFoundException("Employee", change.EmployeeId);
            var changes = JsonSerializer.Deserialize<Dictionary<string, string>>(change.FieldChangesJson) ?? [];
            foreach (var (key, value) in changes)
            {
                switch (key.ToLowerInvariant())
                {
                    case "phone": employee.Phone = value; break;
                    case "personalemail": employee.PersonalEmail = value; break;
                    case "addressline": employee.AddressLine = value; break;
                    case "city": employee.City = value; break;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await notifications.NotifyEmployeeAsync(change.EmployeeId, NotificationType.ProfileChangeDecision,
            request.Approve ? "Profile updated" : "Profile change rejected",
            request.Approve
                ? "Your profile change request was approved and applied."
                : $"Your profile change request was rejected. {request.ReviewNote}".Trim(),
            "/profile", ct);

        return await ToProfileChangeDtoAsync(change, ct);
    }

    public record ReviewCorrectionRequestLite
    {
        public bool Approve { get; init; }
        public string? ReviewNote { get; init; }
    }

    public async Task<PagedResult<ProfileChangeRequestDto>> ListProfileChangesAsync(PagedRequest page, ReviewStatus? status, CancellationToken ct = default)
    {
        user.Ensure(Permissions.HrRequestsHandle);
        var query = db.ProfileChangeRequests.AsNoTracking();
        if (status is { } s) query = query.Where(p => p.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(p => p.CreatedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100)).ToListAsync(ct);

        var dtos = new List<ProfileChangeRequestDto>(items.Count);
        foreach (var p in items) dtos.Add(await ToProfileChangeDtoAsync(p, ct));
        return new PagedResult<ProfileChangeRequestDto>(dtos, page.Page, page.PageSize, total);
    }

    // ─── HR requests ─────────────────────────────────────────────────────────────

    public async Task<HrRequestDto> SubmitHrRequestAsync(SubmitHrRequest request, CancellationToken ct = default)
    {
        await new SubmitHrRequestValidator().ValidateAndThrowAsync(request, ct);
        var employeeId = RequireEmployeeId();

        var hr = new HRRequest
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            Category = request.Category,
            Subject = request.Subject.Trim(),
            Body = request.Body
        };
        db.HRRequests.Add(hr);
        await db.SaveChangesAsync(ct);

        await notifications.NotifyApproversAsync(NotificationType.PendingApprovals,
            "New HR request",
            $"{request.Subject} — from the self-service portal.",
            "/hr-requests", ct);

        return await ToHrRequestDtoAsync(hr, ct);
    }

    public async Task<HrRequestDto> RespondHrRequestAsync(Guid requestId, RespondHrRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.HrRequestsHandle);
        await new RespondHrRequestValidator().ValidateAndThrowAsync(request, ct);

        var hr = await db.HRRequests.FirstOrDefaultAsync(h => h.Id == requestId, ct)
            ?? throw new NotFoundException("HR request", requestId);
        if (hr.Status == ReviewStatus.Approved)
            throw new BusinessRuleException("This request is already closed.");

        hr.Status = request.Approve ? ReviewStatus.Approved : ReviewStatus.Rejected;
        hr.Response = request.Response;
        hr.HandledBy = user.Email;
        hr.ClosedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        await notifications.NotifyEmployeeAsync(hr.EmployeeId, NotificationType.HrRequestResponse,
            request.Approve ? "HR request answered" : "HR request rejected",
            $"{hr.Subject}: {request.Response}", "/self-service", ct);

        return await ToHrRequestDtoAsync(hr, ct);
    }

    public async Task<PagedResult<HrRequestDto>> ListHrRequestsAsync(PagedRequest page, ReviewStatus? status, CancellationToken ct = default)
    {
        var query = db.HRRequests.AsNoTracking();
        if (!user.Has(Permissions.HrRequestsHandle))
        {
            query = query.Where(h => h.EmployeeId == user.EmployeeId);
        }
        if (status is { } s) query = query.Where(h => h.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(h => h.CreatedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100)).ToListAsync(ct);

        var dtos = new List<HrRequestDto>(items.Count);
        foreach (var h in items) dtos.Add(await ToHrRequestDtoAsync(h, ct));
        return new PagedResult<HrRequestDto>(dtos, page.Page, page.PageSize, total);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task<ProfileChangeRequestDto> ToProfileChangeDtoAsync(ProfileChangeRequest p, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.EmployeeId, ct)
            ?? new Domain.Employees.Employee { FirstName = "?", LastName = "" };
        return new ProfileChangeRequestDto
        {
            Id = p.Id,
            EmployeeId = p.EmployeeId,
            EmployeeName = $"{e.FirstName} {e.LastName}",
            FieldChangesJson = p.FieldChangesJson,
            Status = p.Status.ToString(),
            ReviewedBy = p.ReviewedBy,
            ReviewedAtUtc = p.ReviewedAtUtc,
            ReviewNote = p.ReviewNote,
            CreatedAtUtc = p.CreatedAtUtc
        };
    }

    private async Task<HrRequestDto> ToHrRequestDtoAsync(HRRequest h, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == h.EmployeeId, ct)
            ?? new Domain.Employees.Employee { FirstName = "?", LastName = "" };
        return new HrRequestDto
        {
            Id = h.Id,
            EmployeeId = h.EmployeeId,
            EmployeeName = $"{e.FirstName} {e.LastName}",
            Category = h.Category,
            Subject = h.Subject,
            Body = h.Body,
            Status = h.Status.ToString(),
            Response = h.Response,
            HandledBy = h.HandledBy,
            ClosedAtUtc = h.ClosedAtUtc,
            CreatedAtUtc = h.CreatedAtUtc
        };
    }
}
