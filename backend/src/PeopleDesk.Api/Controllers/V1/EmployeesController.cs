using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Employees;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Api.Controllers.V1;

[ApiController]
[Route("api/v1/employees")]
[Authorize]
public class EmployeesController(EmployeeService employees) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<PagedResult<EmployeeListDto>>> List(
        [FromQuery] Guid? departmentId, [FromQuery] Guid? branchId, [FromQuery] EmploymentStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        CancellationToken ct = default)
    {
        return Ok(await employees.ListAsync(PagingExtensions.ToPagedRequest(page, pageSize, search, sortBy, sortDesc), departmentId, branchId, status, ct));
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDetailsDto>> Me(CancellationToken ct) =>
        Ok(await employees.GetOwnAsync(ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<EmployeeDetailsDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await employees.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.EmployeesCreate)]
    public async Task<ActionResult<EmployeeDetailsDto>> Create(CreateEmployeeRequest request, CancellationToken ct) =>
        Ok(await employees.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.EmployeesUpdate)]
    public async Task<ActionResult<EmployeeDetailsDto>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken ct) =>
        Ok(await employees.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/offboard")]
    [HasPermission(Permissions.EmployeesUpdate)]
    public async Task<IActionResult> Offboard(Guid id, OffboardRequest request, CancellationToken ct)
    {
        await employees.OffboardAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.EmployeesDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await employees.DeleteAsync(id, ct);
        return NoContent();
    }

    // ─── Contacts ────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/contacts")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<List<EmployeeContactDto>>> Contacts(Guid id, CancellationToken ct) =>
        Ok(await employees.ListContactsAsync(id, ct));

    [HttpPost("{id:guid}/contacts")]
    [HasPermission(Permissions.EmployeesUpdate)]
    public async Task<ActionResult<EmployeeContactDto>> SaveContact(Guid id, SaveEmployeeContactRequest request, CancellationToken ct) =>
        Ok(await employees.SaveContactAsync(id, request, ct));

    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    [HasPermission(Permissions.EmployeesUpdate)]
    public async Task<IActionResult> DeleteContact(Guid id, Guid contactId, CancellationToken ct)
    {
        await employees.DeleteContactAsync(id, contactId, ct);
        return NoContent();
    }

    // ─── Documents ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/documents")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<List<EmployeeDocumentDto>>> Documents(Guid id, CancellationToken ct) =>
        Ok(await employees.ListDocumentsAsync(id, ct));

    [HttpPost("{id:guid}/documents")]
    [HasPermission(Permissions.EmployeesUpdate)]
    [RequestSizeLimit(10_500_000)]
    public async Task<ActionResult<EmployeeDocumentDto>> UploadDocument(
        Guid id, IFormFile file, [FromForm] DocumentKind kind = DocumentKind.Other,
        [FromForm] string? name = null, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = new { code = "bad_request", message = "No file was uploaded." } });
        await using var stream = file.OpenReadStream();
        return Ok(await employees.UploadDocumentAsync(id, kind, name ?? "", file.FileName, file.ContentType, file.Length, stream, ct));
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/download")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<IActionResult> DownloadDocument(Guid id, Guid documentId, CancellationToken ct)
    {
        var result = await employees.DownloadDocumentAsync(id, documentId, ct);
        if (result is null) return NotFound();
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [HasPermission(Permissions.EmployeesUpdate)]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken ct)
    {
        await employees.DeleteDocumentAsync(id, documentId, ct);
        return NoContent();
    }

    // ─── Employment history ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/history")]
    [HasPermission(Permissions.EmployeesView)]
    public async Task<ActionResult<List<EmploymentHistoryDto>>> History(Guid id, CancellationToken ct) =>
        Ok(await employees.ListHistoryAsync(id, ct));
}
