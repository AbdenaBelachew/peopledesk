using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Payroll;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/payroll")]
[Authorize]
public class PayrollController(
    PayrollService payroll,
    PeopleDesk.Application.Payslips.PayslipService payslips) : ControllerBase
{
    [HttpGet("rule-sets")]
    public async Task<ActionResult> RuleSets(CancellationToken ct) => Ok(await payroll.ListRuleSetsAsync(ct));

    [HttpGet("rule-sets/{id:guid}")]
    public async Task<ActionResult> RuleSet(Guid id, CancellationToken ct) => Ok(await payroll.GetRuleSetAsync(id, ct));

    [HttpGet("periods")]
    public async Task<ActionResult<List<PayrollPeriodDto>>> Periods(CancellationToken ct) =>
        Ok(await payroll.ListPeriodsAsync(ct));

    [HttpPost("periods")]
    [HasPermission(Permissions.PayrollRun)]
    public async Task<ActionResult<PayrollPeriodDto>> CreatePeriod(CreatePeriodRequest request, CancellationToken ct) =>
        Ok(await payroll.CreatePeriodAsync(request, ct));

    [HttpDelete("periods/{id:guid}")]
    [HasPermission(Permissions.PayrollRun)]
    public async Task<IActionResult> DeletePeriod(Guid id, CancellationToken ct)
    {
        await payroll.DeletePeriodAsync(id, ct);
        return NoContent();
    }

    [HttpPost("runs")]
    [HasPermission(Permissions.PayrollRun)]
    public async Task<ActionResult<PayrollRunDto>> CreateRun([FromBody] CreateRunRequest request, CancellationToken ct) =>
        Ok(await payroll.CreateRunAsync(request.PayrollPeriodId, ct));

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<PayrollRunDto>> GetRun(Guid id, CancellationToken ct) =>
        Ok(await payroll.GetRunAsync(id, ct));

    [HttpGet("runs/{id:guid}/items")]
    public async Task<ActionResult<PagedResult<PayrollItemDto>>> GetRunItems(
        Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await payroll.GetRunItemsAsync(id, PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), ct));

    /// <summary>Calculates (or recalculates) the run from salary structures, attendance and adjustments.</summary>
    [HttpPost("runs/{id:guid}/calculate")]
    [HasPermission(Permissions.PayrollRun)]
    public async Task<ActionResult<PayrollRunDto>> Calculate(Guid id, CancellationToken ct) =>
        Ok(await payroll.CalculateAsync(id, ct));

    [HttpPost("runs/{id:guid}/approve")]
    [HasPermission(Permissions.PayrollApprove)]
    public async Task<ActionResult<PayrollRunDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await payroll.ApproveAsync(id, ct));

    /// <summary>Finalizes the run: locks the period, generates + archives payslip PDFs, locks attendance.</summary>
    [HttpPost("runs/{id:guid}/finalize")]
    [HasPermission(Permissions.PayrollFinalize)]
    public async Task<ActionResult<PayrollRunDto>> Finalize(Guid id, CancellationToken ct)
    {
        var run = await payroll.FinalizeAsync(id, ct);
        await payslips.GenerateForRunAsync(id, ct);
        return Ok(run);
    }

    [HttpPost("runs/{id:guid}/reverse")]
    [HasPermission(Permissions.PayrollReverse)]
    public async Task<ActionResult<PayrollRunDto>> Reverse(Guid id, ReverseRunRequest request, CancellationToken ct) =>
        Ok(await payroll.ReverseAsync(id, request, ct));

    [HttpPost("runs/{id:guid}/adjustments")]
    [HasPermission(Permissions.PayrollRun)]
    public async Task<IActionResult> AddAdjustment(Guid id, AddAdjustmentRequest request, CancellationToken ct)
    {
        await payroll.AddAdjustmentAsync(id, request, ct);
        return NoContent();
    }
}

public record CreateRunRequest { public Guid PayrollPeriodId { get; init; } }
