using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Payroll;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/employees/{employeeId:guid}/salary-structures")]
[Authorize]
public class SalaryStructuresController(SalaryStructureService salaries) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.EmployeesViewSensitive)]
    public async Task<ActionResult<List<SalaryStructureDto>>> List(Guid employeeId, CancellationToken ct) =>
        Ok(await salaries.ListAsync(employeeId, ct));

    [HttpPost]
    [HasPermission(Permissions.EmployeesViewSensitive)]
    public async Task<ActionResult<SalaryStructureDto>> Create(Guid employeeId, SaveSalaryStructureRequest request, CancellationToken ct) =>
        Ok(await salaries.CreateAsync(employeeId, request, ct));

    [HttpDelete("{structureId:guid}")]
    [HasPermission(Permissions.EmployeesViewSensitive)]
    public async Task<IActionResult> Delete(Guid employeeId, Guid structureId, CancellationToken ct)
    {
        await salaries.DeleteAsync(employeeId, structureId, ct);
        return NoContent();
    }
}
