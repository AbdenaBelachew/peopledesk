using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Payroll;

/// <summary>
/// Versioned, effective-dated payroll rule configuration for a jurisdiction.
/// Tax brackets and pension rates live here — never hardcoded in engine code.
/// </summary>
public class PayrollRuleSet : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    /// <summary>Jurisdiction code, e.g. "ET" for Ethiopia.</summary>
    public string JurisdictionCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Version { get; set; } = default!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal PensionEmployeeRate { get; set; }   // e.g. 0.07
    public decimal PensionEmployerRate { get; set; }   // e.g. 0.11
    public decimal? PensionCeiling { get; set; }
    public decimal? TaxReliefPerMonth { get; set; }

    public ICollection<PayrollTaxBracket> TaxBrackets { get; set; } = new List<PayrollTaxBracket>();
}

/// <summary>Progressive income-tax bracket. UpperBound null = open-ended. QuickDeduction is the standard shortcut subtraction.</summary>
public class PayrollTaxBracket : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid RuleSetId { get; set; }
    public PayrollRuleSet RuleSet { get; set; } = default!;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    /// <summary>Marginal rate as a fraction (0.10 = 10%).</summary>
    public decimal Rate { get; set; }
    public decimal QuickDeduction { get; set; }
}

public class Payslip : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid PayrollItemId { get; set; }

    public string PayslipNumber { get; set; } = default!;
    public string PeriodName { get; set; } = default!;
    public string Currency { get; set; } = "ETB";

    public decimal GrossPay { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public DateTime IssuedAtUtc { get; set; }
    public string? PublishedBy { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    public string StorageKey { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/pdf";
}
