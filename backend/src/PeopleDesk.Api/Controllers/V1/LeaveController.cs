using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Leave;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/leave")]
[Authorize]
public class LeaveController(LeaveService leave) : ControllerBase
{
    [HttpGet("types")]
    public async Task<ActionResult<List<LeaveTypeDto>>> Types(CancellationToken ct) =>
        Ok(await leave.ListTypesAsync(ct));

    [HttpPost("types")]
    [HasPermission(Permissions.LeaveManage)]
    public async Task<ActionResult<LeaveTypeDto>> SaveType(SaveLeaveTypeRequest request, CancellationToken ct) =>
        Ok(await leave.SaveTypeAsync(request, ct));

    [HttpGet("balances/{employeeId:guid}")]
    public async Task<ActionResult<List<LeaveBalanceDto>>> Balances(Guid employeeId, [FromQuery] int? year, CancellationToken ct) =>
        Ok(await leave.BalancesAsync(employeeId, year, ct));

    [HttpPost("balances/{employeeId:guid}/{leaveTypeId:guid}/adjust")]
    [HasPermission(Permissions.LeaveManage)]
    public async Task<ActionResult<LeaveBalanceDto>> Adjust(Guid employeeId, Guid leaveTypeId,
        [FromBody] AdjustBalanceRequest request, CancellationToken ct) =>
        Ok(await leave.AdjustBalanceAsync(employeeId, leaveTypeId, request.Days, request.Note, ct));

    [HttpGet("transactions/{employeeId:guid}")]
    public async Task<ActionResult<List<LeaveTransactionDto>>> Transactions(
        Guid employeeId, [FromQuery] Guid? leaveTypeId, [FromQuery] int? year, CancellationToken ct) =>
        Ok(await leave.TransactionsAsync(employeeId, leaveTypeId, year, ct));

    // ─── Requests ────────────────────────────────────────────────────────────────

    [HttpGet("requests")]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> Requests(
        [FromQuery] Guid? employeeId, [FromQuery] ReviewStatus? status, [FromQuery] Guid? leaveTypeId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await leave.ListAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), employeeId, status, leaveTypeId, ct));

    [HttpPost("requests")]
    public async Task<ActionResult<LeaveRequestDto>> Create(CreateLeaveRequest request, CancellationToken ct) =>
        Ok(await leave.CreateAsync(request, ct));

    [HttpPost("requests/{id:guid}/approve")]
    [HasPermission(Permissions.LeaveApprove)]
    public async Task<ActionResult<LeaveRequestDto>> Approve(Guid id, ReviewLeaveRequest request, CancellationToken ct) =>
        Ok(await leave.ReviewAsync(id, request with { Approve = true }, ct));

    [HttpPost("requests/{id:guid}/reject")]
    [HasPermission(Permissions.LeaveApprove)]
    public async Task<ActionResult<LeaveRequestDto>> Reject(Guid id, ReviewLeaveRequest request, CancellationToken ct) =>
        Ok(await leave.ReviewAsync(id, request with { Approve = false }, ct));

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await leave.CancelAsync(id, ct));

    // ─── Calendar ────────────────────────────────────────────────────────────────

    [HttpGet("calendar")]
    [HasPermission(Permissions.LeaveViewAll)]
    public async Task<ActionResult<List<LeaveCalendarEntryDto>>> Calendar(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? departmentId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? new DateOnly(today.Year, today.Month, 1);
        var t = to ?? f.AddMonths(1).AddDays(-1);
        return Ok(await leave.CalendarAsync(f, t, departmentId, ct));
    }
}

public record AdjustBalanceRequest { public decimal Days { get; init; } public string? Note { get; init; } }
