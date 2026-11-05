using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Application.Payroll;

public class SalaryStructureService(IAppDbContext db, ICurrentUser user, IDateTime clock)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    public async Task<List<SalaryStructureDto>> ListAsync(Guid employeeId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesViewSensitive);
        var structures = await db.SalaryStructures.AsNoTracking()
            .Include(s => s.Components)
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(ct);
        return structures.Select(ToDto).ToList();
    }

    public async Task<SalaryStructureDto> CreateAsync(Guid employeeId, SaveSalaryStructureRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesViewSensitive);
        await new SaveSalaryStructureRequestValidator().ValidateAndThrowAsync(request, ct);

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId);
        _ = employee;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Close any structure whose validity is open-ended from the new EffectiveFrom.
        var overlapping = await db.SalaryStructures
            .Where(s => s.EmployeeId == employeeId && s.EffectiveTo == null && s.EffectiveFrom < request.EffectiveFrom)
            .ToListAsync(ct);
        foreach (var s in overlapping)
            s.EffectiveTo = request.EffectiveFrom.AddDays(-1);

        var structure = new SalaryStructure
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            EffectiveFrom = request.EffectiveFrom,
            Currency = request.Currency.ToUpperInvariant(),
            Notes = request.Notes
        };
        foreach (var c in request.Components)
        {
            structure.Components.Add(new SalaryComponent
            {
                OrganizationId = OrgId,
                SalaryStructureId = structure.Id,
                Kind = c.Kind,
                Code = c.Code.Trim().ToUpperInvariant(),
                Label = c.Label.Trim(),
                Type = c.Type,
                CalculationType = c.CalculationType,
                Amount = c.Amount,
                IsTaxable = c.IsTaxable,
                IsActive = c.IsActive
            });
        }

        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(structure);
    }

    public async Task DeleteAsync(Guid employeeId, Guid structureId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesViewSensitive);
        var structure = await db.SalaryStructures
            .FirstOrDefaultAsync(s => s.Id == structureId && s.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("Salary structure", structureId);

        // Guard: structures referenced by finalized payroll items must remain (audit trail).
        var usedByFinalized = await db.PayrollItems.AsNoTracking()
            .AnyAsync(i => i.SalaryStructureId == structureId && i.PayrollRun!.Status == PayrollRunStatus.Finalized, ct);
        if (usedByFinalized)
            throw new BusinessRuleException("This salary structure is referenced by finalized payslips and cannot be deleted.");

        db.SalaryStructures.Remove(structure);
        await db.SaveChangesAsync(ct);
    }

    private static SalaryStructureDto ToDto(SalaryStructure s) => new()
    {
        Id = s.Id,
        EmployeeId = s.EmployeeId,
        EffectiveFrom = s.EffectiveFrom,
        EffectiveTo = s.EffectiveTo,
        Currency = s.Currency,
        Notes = s.Notes,
        Components = [.. s.Components.Select(c => new SalaryComponentDto
        {
            Id = c.Id, Kind = c.Kind, Code = c.Code, Label = c.Label, Type = c.Type,
            CalculationType = c.CalculationType, Amount = c.Amount, IsTaxable = c.IsTaxable, IsActive = c.IsActive
        })]
    };
}
