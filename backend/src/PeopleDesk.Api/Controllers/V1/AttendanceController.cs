using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Attendance;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public class AttendanceController(AttendanceService attendance) : ControllerBase
{
    /// <summary>Clock in for the signed-in employee (self-service).</summary>
    [HttpPost("clock-in")]
    public async Task<ActionResult<ClockActionDto>> ClockIn(CancellationToken ct) =>
        Ok(await attendance.ClockInAsync(ct));

    /// <summary>Clock out for the signed-in employee (self-service).</summary>
    [HttpPost("clock-out")]
    public async Task<ActionResult<ClockActionDto>> ClockOut(CancellationToken ct) =>
        Ok(await attendance.ClockOutAsync(ct));

    [HttpGet("today")]
    public async Task<ActionResult<AttendanceDto?>> Today(CancellationToken ct) =>
        Ok(await attendance.GetTodayAsync(ct));

    [HttpGet]
    [HasPermission(Permissions.AttendanceViewAll)]
    public async Task<ActionResult<PagedResult<AttendanceDto>>> List(
        [FromQuery] Guid? employeeId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await attendance.ListAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), employeeId, from, to, ct));

    [HttpPost("manual")]
    [HasPermission(Permissions.AttendanceManage)]
    public async Task<ActionResult<AttendanceDto>> ManualSave(ManualAttendanceRequest request, CancellationToken ct) =>
        Ok(await attendance.ManualSaveAsync(request, ct));

    // ─── Corrections ─────────────────────────────────────────────────────────────

    [HttpPost("corrections")]
    public async Task<ActionResult<CorrectionRequestDto>> CreateCorrection(CreateCorrectionRequest request, CancellationToken ct) =>
        Ok(await attendance.CreateCorrectionAsync(request, ct));

    [HttpGet("corrections")]
    public async Task<ActionResult<PagedResult<CorrectionRequestDto>>> Corrections(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false, CancellationToken ct = default) =>
        Ok(await attendance.ListCorrectionsAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), status, ct));

    [HttpPost("corrections/{id:guid}/review")]
    [HasPermission(Permissions.AttendanceApprove)]
    public async Task<ActionResult<CorrectionRequestDto>> ReviewCorrection(Guid id, ReviewCorrectionRequest request, CancellationToken ct) =>
        Ok(await attendance.ReviewCorrectionAsync(id, request, ct));

    // ─── Summaries ───────────────────────────────────────────────────────────────

    [HttpGet("summary/{employeeId:guid}/{year:int}/{month:int}")]
    public async Task<ActionResult<MonthlyAttendanceSummaryDto>> Summary(
        Guid employeeId, int year, int month, CancellationToken ct) =>
        Ok(await attendance.MonthlySummaryAsync(employeeId, year, month, ct));
}
