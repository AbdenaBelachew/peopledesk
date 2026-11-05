using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Payroll;

public record PayrollPeriodDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public PayrollPeriodType Type { get; init; }
    public int Year { get; init; }
    public int? Month { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateOnly? PayDate { get; init; }
    public bool Closed { get; init; }
    public Guid? ActiveRunId { get; init; }
    public string? RunStatus { get; init; }
    public decimal? TotalNet { get; init; }
}

public record CreatePeriodRequest
{
    public PayrollPeriodType Type { get; init; } = PayrollPeriodType.Monthly;
    public int Year { get; init; }
    public int Month { get; init; }
    public DateOnly? PayDate { get; init; }
}

public record PayrollRunDto
{
    public Guid Id { get; init; }
    public Guid PayrollPeriodId { get; init; }
    public string PeriodName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public decimal TotalGross { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal TotalNet { get; init; }
    public int EmployeeCount { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
    public string? CalculatedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public string? FinalizedBy { get; init; }
    public DateTime? FinalizedAtUtc { get; init; }
    public string? ReversalNote { get; init; }
    public IReadOnlyList<string> SkippedEmployees { get; init; } = [];
}

public record PayrollItemDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public string EmployeeNumber { get; init; } = default!;
    public string Currency { get; init; } = default!;
    public int DaysWorked { get; init; }
    public decimal UnpaidLeaveDays { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal BasicSalary { get; init; }
    public decimal TotalEarnings { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal NetSalary { get; init; }
    public IReadOnlyList<PayrollLineDto> Earnings { get; init; } = [];
    public IReadOnlyList<PayrollLineDto> Deductions { get; init; } = [];
}

public record PayrollLineDto
{
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public string Type { get; init; } = default!;
    public decimal Amount { get; init; }
}

public record AddAdjustmentRequest
{
    public Guid EmployeeId { get; init; }
    public PayComponentKind Kind { get; init; }
    public string Code { get; init; } = default!;
    public string Label { get; init; } = default!;
    public decimal Amount { get; init; }
    public string? Note { get; init; }
}

public record ReverseRunRequest { public string Note { get; init; } = default!; }

public class CreatePeriodRequestValidator : AbstractValidator<CreatePeriodRequest>
{
    public CreatePeriodRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
