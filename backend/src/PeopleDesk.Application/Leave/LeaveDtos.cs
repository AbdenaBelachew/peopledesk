using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Leave;

public record LeaveTypeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string? ColorHex { get; init; }
    public bool IsPaid { get; init; }
    public bool RequiresApproval { get; init; }
    public string? Description { get; init; }
    public decimal AnnualEntitlementDays { get; init; }
    public string AccrualFrequency { get; init; } = default!;
    public decimal CarryOverMaxDays { get; init; }
    public int MinNoticeDays { get; init; }
    public int MaxConsecutiveDays { get; init; }
    public bool AllowHalfDays { get; init; }
}

public record SaveLeaveTypeRequest
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string? ColorHex { get; init; }
    public bool IsPaid { get; init; } = true;
    public bool RequiresApproval { get; init; } = true;
    public string? Description { get; init; }
    public decimal AnnualEntitlementDays { get; init; } = 0;
    public LeaveAccrualFrequency AccrualFrequency { get; init; } = LeaveAccrualFrequency.YearlyUpfront;
    public decimal CarryOverMaxDays { get; init; }
    public int MinNoticeDays { get; init; }
    public int MaxConsecutiveDays { get; init; } = 90;
    public bool AllowHalfDays { get; init; }
}

public record LeaveBalanceDto
{
    public Guid LeaveTypeId { get; init; }
    public string TypeName { get; init; } = default!;
    public string TypeCode { get; init; } = default!;
    public string? ColorHex { get; init; }
    public int Year { get; init; }
    public decimal EntitledDays { get; init; }
    public decimal CarriedOverDays { get; init; }
    public decimal AccruedDays { get; init; }
    public decimal UsedDays { get; init; }
    public decimal PendingDays { get; init; }
    public decimal AdjustedDays { get; init; }
    public decimal Available => EntitledDays + CarriedOverDays + AccruedDays + AdjustedDays - UsedDays - PendingDays;
}

public record LeaveRequestDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public string EmployeeNumber { get; init; } = default!;
    public Guid LeaveTypeId { get; init; }
    public string TypeName { get; init; } = default!;
    public string? ColorHex { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal Days { get; init; }
    public bool IsPaid { get; init; }
    public string? Reason { get; init; }
    public string Status { get; init; } = default!;
    public string? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? ReviewNote { get; init; }
    public bool HrOverride { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
}

public record CreateLeaveRequest
{
    public Guid EmployeeId { get; init; }
    public Guid LeaveTypeId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsHalfDay { get; init; }
    public string? Reason { get; init; }
}

public record ReviewLeaveRequest
{
    public bool Approve { get; init; }
    public string? ReviewNote { get; init; }
    /// <summary>HR override: bypass the manager chain (audit-logged).</summary>
    public bool IsHrOverride { get; init; }
}

public record LeaveTransactionDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid LeaveTypeId { get; init; }
    public int Year { get; init; }
    public string Type { get; init; } = default!;
    public decimal Days { get; init; }
    public decimal BalanceAfter { get; init; }
    public Guid? LeaveRequestId { get; init; }
    public string? Description { get; init; }
    public string? Actor { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record LeaveCalendarEntryDto
{
    public Guid RequestId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public Guid LeaveTypeId { get; init; }
    public string TypeName { get; init; } = default!;
    public string? ColorHex { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string Status { get; init; } = default!;
}

public class SaveLeaveTypeRequestValidator : AbstractValidator<SaveLeaveTypeRequest>
{
    public SaveLeaveTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Z0-9_]{2,20}$")
            .WithMessage("Code must be uppercase letters/digits/underscore (2-20 chars).");
        RuleFor(x => x.AnnualEntitlementDays).InclusiveBetween(0, 365);
        RuleFor(x => x.ColorHex).Matches("^#[0-9a-fA-F]{6}$").When(x => !string.IsNullOrEmpty(x.ColorHex));
    }
}

public class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequest>
{
    public CreateLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveTypeId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().Must((r, end) => end >= r.StartDate)
            .WithMessage("End date must be on or after start date.");
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}
