using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Platform;
using PeopleDesk.Application.Reports;
using PeopleDesk.Application.SelfService;
using PeopleDesk.Domain.Common;
using PagedRequest = PeopleDesk.Application.Common.PagedRequest;

namespace PeopleDesk.Api.Controllers.V1;

// ─── Self-service portal ─────────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/self-service")]
[Authorize]
public class SelfServiceController(SelfServiceService self) : ControllerBase
{
    [HttpPost("profile-changes")]
    public async Task<ActionResult<ProfileChangeRequestDto>> SubmitProfileChange(SubmitProfileChangeRequest request, CancellationToken ct) =>
        Ok(await self.SubmitProfileChangeAsync(request, ct));

    [HttpGet("profile-changes")]
    [HasPermission(Permissions.HrRequestsHandle)]
    public async Task<ActionResult<PagedResult<ProfileChangeRequestDto>>> ProfileChanges(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await self.ListProfileChangesAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), status, ct));

    [HttpPost("profile-changes/{id:guid}/review")]
    [HasPermission(Permissions.HrRequestsHandle)]
    public async Task<ActionResult<ProfileChangeRequestDto>> ReviewProfileChange(Guid id,
        SelfServiceService.ReviewCorrectionRequestLite request, CancellationToken ct) =>
        Ok(await self.ReviewProfileChangeAsync(id, request, ct));

    [HttpPost("hr-requests")]
    public async Task<ActionResult<HrRequestDto>> SubmitHrRequest(SubmitHrRequest request, CancellationToken ct) =>
        Ok(await self.SubmitHrRequestAsync(request, ct));

    [HttpGet("hr-requests")]
    public async Task<ActionResult<PagedResult<HrRequestDto>>> HrRequests(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await self.ListHrRequestsAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), status, ct));

    [HttpPost("hr-requests/{id:guid}/respond")]
    [HasPermission(Permissions.HrRequestsHandle)]
    public async Task<ActionResult<HrRequestDto>> RespondHrRequest(Guid id, RespondHrRequest request, CancellationToken ct) =>
        Ok(await self.RespondHrRequestAsync(id, request, ct));
}

// ─── Reports ─────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(ReportsService reports) : ControllerBase
{
    [HttpGet("headcount")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<ActionResult<HeadcountReportDto>> Headcount(CancellationToken ct) =>
        Ok(await reports.HeadcountAsync(ct));

    [HttpGet("attendance-trends")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<ActionResult<AttendanceTrendDto>> AttendanceTrends(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? today.AddDays(-30);
        var t = to ?? today;
        return Ok(await reports.AttendanceTrendAsync(f, t, ct));
    }

    [HttpGet("leave-utilization")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<ActionResult<LeaveUtilizationDto>> LeaveUtilization([FromQuery] int? year, CancellationToken ct) =>
        Ok(await reports.LeaveUtilizationAsync(year ?? DateTime.UtcNow.Year, ct));

    [HttpGet("payroll-expenses")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<ActionResult<PayrollExpenseDto>> PayrollExpenses(CancellationToken ct) =>
        Ok(await reports.PayrollExpensesAsync(ct));

    [HttpGet("turnover")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<ActionResult<TurnoverReportDto>> Turnover([FromQuery] int? year, CancellationToken ct) =>
        Ok(await reports.TurnoverAsync(year ?? DateTime.UtcNow.Year, ct));

    // Exports
    [HttpGet("headcount/export")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> ExportHeadcount([FromQuery] bool format = false, CancellationToken ct = default)
    {
        var (content, contentType, fileName) = await reports.ExportHeadcountAsync(format, ct);
        return File(content, contentType, fileName);
    }

    [HttpGet("attendance/export")]
    [HasPermission(Permissions.ReportsView)]
    public async Task<IActionResult> ExportAttendance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] bool format = false, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? today.AddDays(-30);
        var t = to ?? today;
        var (content, contentType, fileName) = await reports.ExportAttendanceAsync(f, t, format, ct);
        return File(content, contentType, fileName);
    }

    [HttpGet("payroll/{runId:guid}/export")]
    [HasPermission(Permissions.PayrollView)]
    public async Task<IActionResult> ExportPayroll(Guid runId, [FromQuery] bool format = false, CancellationToken ct = default)
    {
        var (content, contentType, fileName) = await reports.ExportPayrollAsync(runId, format, ct);
        return File(content, contentType, fileName);
    }
}

// ─── SaaS platform administration ────────────────────────────────────────────────

[ApiController]
[Route("api/v1/platform")]
[Authorize]
public class PlatformController(PlatformService platform) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult> Stats(CancellationToken ct) => Ok(await platform.StatsAsync(ct));

    [HttpGet("tenants")]
    public async Task<ActionResult<PagedResult<PlatformService.TenantDto>>> Tenants(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, CancellationToken ct = default) =>
        Ok(await platform.ListTenantsAsync(PagingExtensions.ToPagedRequest(page, pageSize, search), ct));

    [HttpPost("tenants/{orgId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid orgId, CancellationToken ct)
    {
        await platform.SuspendTenantAsync(orgId, suspend: true, ct);
        return NoContent();
    }

    [HttpPost("tenants/{orgId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid orgId, CancellationToken ct)
    {
        await platform.SuspendTenantAsync(orgId, suspend: false, ct);
        return NoContent();
    }

    [HttpGet("plans")]
    public async Task<ActionResult<List<PlatformService.PlanDto>>> Plans(CancellationToken ct) =>
        Ok(await platform.ListPlansAsync(ct));

    [HttpPost("plans")]
    public async Task<ActionResult<PlatformService.PlanDto>> SavePlan(PlatformService.SavePlanRequest request, CancellationToken ct) =>
        Ok(await platform.SavePlanAsync(request, ct));

    [HttpPost("tenants/{orgId:guid}/subscription")]
    public async Task<ActionResult> UpdateSubscription(Guid orgId, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct) =>
        Ok(await platform.UpdateSubscriptionAsync(orgId, request.PlanId, request.Status, ct));
}

public record UpdateSubscriptionRequest
{
    public Guid PlanId { get; init; }
    public string Status { get; init; } = default!;
}
