using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Payroll;

public record SalaryStructureDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string Currency { get; init; } = default!;
    public string? Notes { get; init; }
    public IReadOnlyList<SalaryComponentDto> Components { get; init; } = [];
}

public record SalaryComponentDto
{
    public Guid Id { get; init; }
    public PayComponentKind Kind { get; init; }
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public SalaryComponentType Type { get; init; }
    public SalaryCalculationType CalculationType { get; init; }
    public decimal Amount { get; init; }
    public bool IsTaxable { get; init; }
    public bool IsActive { get; init; }
}

public record SaveSalaryStructureRequest
{
    public DateOnly EffectiveFrom { get; init; }
    public string Currency { get; init; } = "ETB";
    public string? Notes { get; init; }
    public IReadOnlyList<SaveSalaryComponentRequest> Components { get; init; } = [];
}

public record SaveSalaryComponentRequest
{
    public PayComponentKind Kind { get; init; }
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public SalaryComponentType Type { get; init; }
    public SalaryCalculationType CalculationType { get; init; } = SalaryCalculationType.FixedAmount;
    public decimal Amount { get; init; }
    public bool IsTaxable { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

public class SaveSalaryStructureRequestValidator : AbstractValidator<SaveSalaryStructureRequest>
{
    public SaveSalaryStructureRequestValidator()
    {
        RuleFor(x => x.EffectiveFrom).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Components).NotEmpty().WithMessage("At least one component (basic salary) is required.");
        RuleFor(x => x.Components).Must(c => c.Count(x => x.Type == SalaryComponentType.Basic) == 1)
            .WithMessage("Exactly one BASIC component is required.");
        RuleForEach(x => x.Components).ChildRules(comp =>
        {
            comp.RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
            comp.RuleFor(c => c.Label).NotEmpty().MaximumLength(60);
            comp.RuleFor(c => c.Amount).GreaterThan(0);
        });
    }
}
