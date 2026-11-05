using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Leave;

public class LeaveType : Entity, IOrganizationScoped, ISoftDeletable
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? ColorHex { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class LeavePolicy : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = default!;

    public decimal AnnualEntitlementDays { get; set; } = 20;
    public LeaveAccrualFrequency AccrualFrequency { get; set; } = LeaveAccrualFrequency.YearlyUpfront;
    public decimal CarryOverMaxDays { get; set; }
    public int ProbationMonthsBeforeEligible { get; set; }
    public int MinNoticeDays { get; set; }
    public int MaxConsecutiveDays { get; set; } = 90;
    public bool AllowHalfDays { get; set; }
}

public class LeaveBalance : Entity, IOrganizationScoped, IHasConcurrencyToken
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }

    public decimal EntitledDays { get; set; }
    public decimal CarriedOverDays { get; set; }
    public decimal AccruedDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal PendingDays { get; set; }
    public decimal AdjustedDays { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    /// <summary>Total days currently available to request (entitlement minus usage/pending).</summary>
    public decimal Available => EntitledDays + CarriedOverDays + AccruedDays + AdjustedDays - UsedDays - PendingDays;
}

/// <summary>Append-only ledger. Every balance mutation writes one row; BalanceAfter mirrors the resulting balance.</summary>
public class LeaveTransaction : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }

    public LeaveTransactionType Type { get; set; }
    /// <summary>Signed delta applied to the balance (positive adds availability, negative consumes).</summary>
    public decimal Days { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? LeaveRequestId { get; set; }
    public string? Description { get; set; }
    public string? Actor { get; set; }
}

public class LeaveRequest : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = default!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    public bool IsHalfDay { get; set; }
    public string? Reason { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>HR override audit trail (approval/rejection outside the manager chain).</summary>
    public string? HrOverrideBy { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
