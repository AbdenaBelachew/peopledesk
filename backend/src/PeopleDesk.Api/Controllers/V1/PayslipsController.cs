using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Payslips;
using PeopleDesk.Application.Payroll;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/payslips")]
[Authorize]
public class PayslipsController(PayslipService payslips) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PayslipService.PayslipDto>>> List(
        [FromQuery] Guid? employeeId, [FromQuery] Guid? periodId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default) =>
        Ok(await payslips.ListAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), employeeId, periodId, ct));

    /// <summary>Downloads the archived PDF. Employees may only download their own payslips.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (pdf, payslip) = await payslips.DownloadAsync(id, ct);
        return File(pdf, "application/pdf",
            $"{payslip.PayslipNumber}.pdf");
    }

    /// <summary>Publishes payslips for a finalized run (email + in-app notifications).</summary>
    [HttpPost("runs/{runId:guid}/publish")]
    [HasPermission(Permissions.PayslipsPublish)]
    public async Task<IActionResult> Publish(Guid runId, CancellationToken ct) =>
        Ok(new { published = await payslips.PublishAsync(runId, ct) });
}
