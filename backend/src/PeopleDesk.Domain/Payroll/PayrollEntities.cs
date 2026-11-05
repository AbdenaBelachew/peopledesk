using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Payroll;

public class SalaryStructure : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Currency { get; set; } = "ETB";
    public Guid? PayrollRuleSetId { get; set; }
    public string? Notes { get; set; }

    public ICollection<SalaryComponent> Components { get; set; } = new List<SalaryComponent>();
}

public class SalaryComponent : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid SalaryStructureId { get; set; }
    public SalaryStructure SalaryStructure { get; set; } = default!;

    public PayComponentKind Kind { get; set; }
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public SalaryComponentType Type { get; set; } = SalaryComponentType.Other;
    public SalaryCalculationType CalculationType { get; set; } = SalaryCalculationType.FixedAmount;

    /// <summary>Fixed amount, or percentage when CalculationType = PercentOfBasic (e.g. 10 = 10%).</summary>
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class PayrollPeriod : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;          // e.g. "2026-11"
    public PayrollPeriodType Type { get; set; } = PayrollPeriodType.Monthly;
    public int Year { get; set; }
    public int? Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly? PayDate { get; set; }
    public bool Closed { get; set; }

    public PayrollRun? ActiveRun { get; set; }
}

public class PayrollRun : Entity, IOrganizationScoped, IHasConcurrencyToken
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public PayrollPeriod Period { get; set; } = default!;

    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    public DateTime? CalculatedAtUtc { get; set; }
    public string? CalculatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? FinalizedBy { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public string? ReversedBy { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversalNote { get; set; }

    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public int EmployeeCount { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public ICollection<PayrollItem> Items { get; set; } = new List<PayrollItem>();
    public ICollection<PayrollRunAdjustment> Adjustments { get; set; } = new List<PayrollRunAdjustment>();
}

/// <summary>Per-employee manual additions applied during calculation (bonuses, commissions, one-off deductions).</summary>
public class PayrollRunAdjustment : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public PayComponentKind Kind { get; set; }
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class PayrollItem : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = default!;
    public Guid EmployeeId { get; set; }

    public Guid SalaryStructureId { get; set; }
    public string Currency { get; set; } = "ETB";

    // Attendance-derived inputs (snapshots for auditability)
    public int DaysWorked { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
    public decimal OvertimeHours { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }

    public ICollection<PayrollEarning> Earnings { get; set; } = new List<PayrollEarning>();
    public ICollection<PayrollDeduction> Deductions { get; set; } = new List<PayrollDeduction>();
}

public class PayrollEarning : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollItemId { get; set; }
    public PayrollItem PayrollItem { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public SalaryComponentType Type { get; set; }
    public decimal Amount { get; set; }
}

public class PayrollDeduction : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollItemId { get; set; }
    public PayrollItem PayrollItem { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Label { get; set; } = default!;
    public SalaryComponentType Type { get; set; }
    public decimal Amount { get; set; }
}
