using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Organizations;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController(OrganizationService orgs) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<OrganizationDto>> Current(CancellationToken ct) =>
        Ok(await orgs.GetCurrentAsync(ct));

    [HttpPut("current")]
    [HasPermission(Permissions.OrgUpdate)]
    public async Task<ActionResult<OrganizationDto>> Update(UpdateOrganizationRequest request, CancellationToken ct) =>
        Ok(await orgs.UpdateAsync(request, ct));

    [HttpGet("settings")]
    public async Task<ActionResult<SettingsDto>> Settings(CancellationToken ct) =>
        Ok(await orgs.GetSettingsAsync(ct));

    [HttpPut("settings")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<ActionResult<SettingsDto>> UpdateSettings(UpdateSettingsRequest request, CancellationToken ct) =>
        Ok(await orgs.UpdateSettingsAsync(request, ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<OrgDashboardDto>> Dashboard(CancellationToken ct) =>
        Ok(await orgs.GetDashboardAsync(ct));

    // ─── Structure ───────────────────────────────────────────────────────────────

    [HttpGet("departments")]
    public async Task<ActionResult<List<StructureItemDto>>> Departments(CancellationToken ct) =>
        Ok(await orgs.ListDepartmentsAsync(ct));

    [HttpPost("departments")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<StructureItemDto>> CreateDepartment(StructureItemRequest request, CancellationToken ct) =>
        Ok(await orgs.CreateDepartmentAsync(request, ct));

    [HttpPut("departments/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> UpdateDepartment(Guid id, StructureItemRequest request, CancellationToken ct)
    {
        await orgs.UpdateDepartmentAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("departments/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct)
    {
        await orgs.DeleteDepartmentAsync(id, ct);
        return NoContent();
    }

    [HttpGet("branches")]
    public async Task<ActionResult<List<StructureItemDto>>> Branches(CancellationToken ct) =>
        Ok(await orgs.ListBranchesAsync(ct));

    [HttpPost("branches")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<StructureItemDto>> CreateBranch(StructureItemRequest request, CancellationToken ct) =>
        Ok(await orgs.CreateBranchAsync(request, ct));

    [HttpPut("branches/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> UpdateBranch(Guid id, StructureItemRequest request, CancellationToken ct)
    {
        await orgs.UpdateBranchAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("branches/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken ct)
    {
        await orgs.DeleteBranchAsync(id, ct);
        return NoContent();
    }

    [HttpGet("positions")]
    public async Task<ActionResult<List<StructureItemDto>>> Positions(CancellationToken ct) =>
        Ok(await orgs.ListPositionsAsync(ct));

    [HttpPost("positions")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<StructureItemDto>> CreatePosition(StructureItemRequest request, CancellationToken ct) =>
        Ok(await orgs.CreatePositionAsync(request, ct));

    [HttpPut("positions/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> UpdatePosition(Guid id, StructureItemRequest request, CancellationToken ct)
    {
        await orgs.UpdatePositionAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("positions/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> DeletePosition(Guid id, CancellationToken ct)
    {
        await orgs.DeletePositionAsync(id, ct);
        return NoContent();
    }

    [HttpGet("employment-types")]
    public async Task<ActionResult<List<StructureItemDto>>> EmploymentTypes(CancellationToken ct) =>
        Ok(await orgs.ListEmploymentTypesAsync(ct));

    [HttpPost("employment-types")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<StructureItemDto>> CreateEmploymentType(StructureItemRequest request, CancellationToken ct) =>
        Ok(await orgs.CreateEmploymentTypeAsync(request, ct));

    [HttpPut("employment-types/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> UpdateEmploymentType(Guid id, StructureItemRequest request, CancellationToken ct)
    {
        await orgs.UpdateEmploymentTypeAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("employment-types/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> DeleteEmploymentType(Guid id, CancellationToken ct)
    {
        await orgs.DeleteEmploymentTypeAsync(id, ct);
        return NoContent();
    }

    // ─── Work schedules & holidays ───────────────────────────────────────────────

    [HttpGet("work-schedules")]
    public async Task<ActionResult<List<WorkScheduleDto>>> WorkSchedules(CancellationToken ct) =>
        Ok(await orgs.ListWorkSchedulesAsync(ct));

    [HttpPost("work-schedules")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<WorkScheduleDto>> SaveWorkSchedule(SaveWorkScheduleRequest request, CancellationToken ct) =>
        Ok(await orgs.SaveWorkScheduleAsync(request, ct));

    [HttpGet("holidays")]
    public async Task<ActionResult<List<HolidayDto>>> Holidays([FromQuery] int? year, CancellationToken ct) =>
        Ok(await orgs.ListHolidaysAsync(year, ct));

    [HttpPost("holidays")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<ActionResult<HolidayDto>> SaveHoliday(SaveHolidayRequest request, CancellationToken ct) =>
        Ok(await orgs.SaveHolidayAsync(request, ct));

    [HttpDelete("holidays/{id:guid}")]
    [HasPermission(Permissions.StructureManage)]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct)
    {
        await orgs.DeleteHolidayAsync(id, ct);
        return NoContent();
    }

    // ─── Announcements ───────────────────────────────────────────────────────────

    [HttpGet("announcements")]
    public async Task<ActionResult<List<AnnouncementDto>>> Announcements([FromQuery] bool includeExpired = false, CancellationToken ct = default) =>
        Ok(await orgs.ListAnnouncementsAsync(includeExpired, ct));

    [HttpPost("announcements")]
    [HasPermission(Permissions.AnnouncementsManage)]
    public async Task<ActionResult<AnnouncementDto>> SaveAnnouncement(SaveAnnouncementRequest request, CancellationToken ct) =>
        Ok(await orgs.SaveAnnouncementAsync(request, ct));
}
