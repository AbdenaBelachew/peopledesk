namespace PeopleDesk.Domain.Payroll;

/// <summary>
/// Pure progressive income-tax computation from a bracket table. Jurisdiction-agnostic:
/// brackets define the country rules; this class only applies them. All money is decimal.
/// Supports both the shortcut method (rate × income − quick deduction) and marginal-band summation.
/// </summary>
public static class IncomeTaxCalculator
{
    public sealed record Bracket(decimal LowerBound, decimal? UpperBound, decimal Rate, decimal QuickDeduction);

    /// <summary>Shortcut method: applies the rate and quick deduction of the TOP bracket containing the income.</summary>
    public static decimal Compute(decimal taxableIncome, IEnumerable<Bracket> bracketsOrderedAscending)
    {
        ArgumentNullException.ThrowIfNull(bracketsOrderedAscending);
        var ordered = bracketsOrderedAscending.OrderBy(b => b.LowerBound).ToList();
        if (ordered.Count == 0) return 0m;

        // Scan from the top down: the first bracket whose lower bound is below the income is the
        // taxpayer's marginal band. (Scanning ascending would wrongly match the 0% band.)
        foreach (var b in ordered.OrderByDescending(x => x.LowerBound))
        {
            if (taxableIncome > b.LowerBound)
            {
                return Round2(taxableIncome * b.Rate - b.QuickDeduction);
            }
        }
        return 0m; // below the first bracket
    }

    /// <summary>Marginal-band summation. Equivalent to the shortcut method when quick deductions are consistent.</summary>
    public static decimal ComputeMarginal(decimal taxableIncome, IEnumerable<Bracket> bracketsOrderedAscending)
    {
        var ordered = bracketsOrderedAscending.OrderBy(b => b.LowerBound).ToList();
        var tax = 0m;
        var cursor = 0m;

        foreach (var b in ordered)
        {
            if (taxableIncome <= cursor) break;
            var upper = b.UpperBound ?? decimal.MaxValue;
            var bandTop = Math.Min(taxableIncome, upper);
            if (bandTop > cursor)
            {
                tax += (bandTop - cursor) * b.Rate;
                cursor = bandTop;
            }
        }
        return Round2(tax);
    }

    public static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Validates that a bracket table is coherent (ascending, non-overlapping, open-ended last band).</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<Bracket> brackets)
    {
        var errors = new List<string>();
        var ordered = brackets.OrderBy(b => b.LowerBound).ToList();

        if (ordered.Count > 0 && ordered[0].LowerBound != 0m)
            errors.Add("First bracket must start at 0.");

        for (var i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            if (b.Rate < 0m || b.Rate > 1m)
                errors.Add($"Bracket {i + 1}: rate must be between 0 and 1.");
            if (b.UpperBound is { } up && up <= b.LowerBound)
                errors.Add($"Bracket {i + 1}: upper bound must exceed lower bound.");
            if (i > 0 && ordered[i - 1].UpperBound is { } prevUp && prevUp < b.LowerBound)
                errors.Add($"Bracket {i + 1}: gap between brackets is not allowed.");
            if (i == ordered.Count - 1 && b.UpperBound is not null)
                errors.Add("Last bracket must be open-ended (null upper bound).");
        }

        return errors;
    }
}

/// <summary>
/// ETHIOPIA PAYROLL RULES — versioned seed data (module: Payroll.Ethiopia).
///
/// CURRENT SCHEDULE — Income Tax Amendment Proclamation No. 1395/2025 (approved 2025-07-17;
/// effective for the Ethiopian FY 2025/26). Monthly employment income bands (ETB):
///   0–2,000 → 0% | 2,001–4,000 → 15% | 4,001–7,000 → 20% | 7,001–10,000 → 25% | 10,001–14,000 → 30% | >14,000 → 35%
/// Sources (verified 2026-11-04):
///   • EY Global Tax News — globaltaxnews.ey.com/news/2025-2542
///   • Chambers & Partners proclamation analysis — chambers.com/articles/what-s-changed-under-ethiopia-s-new-income-tax-amendment-proclamation-no-1395-2025
///   • TaxDev analysis — taxdev.org/news-events/ethiopias-revised-income-tax-explained
/// The quick deductions below are derived so that rate × income − quick deduction equals the
/// marginal-band sum exactly (quick = Σ prior bands' flat tax − band lower bound × rate).
///
/// LEGACY SCHEDULE — Proclamation No. 979/2016 as amended by No. 1157/2019, kept for effective-dated
/// runs before the 2025 reform. EffectiveFrom/To bound both versions; the engine selects by period date.
///
/// PENSION — Private Organization Employees' Pension Proclamation No. 746/2009: 7% employee / 11% employer.
/// VERIFY pension ceilings and the exact 2025 effective date against official sources before production use.
/// Rules are DATA, not code — to change them, seed a new PayrollRuleSet version; never edit the engine.
/// </summary>
public static class EthiopiaPayroll
{
    public const string JurisdictionCode = "ET";

    // Current: Proclamation 1395/2025
    public const string Version2025 = "ET-2025-1395";
    public static readonly DateOnly EffectiveFrom2025 = new(2025, 7, 1);

    // Legacy: Proclamation 979/2016 (amended 1157/2019)
    public const string Version2016 = "ET-2016-979-amended-2019";
    public static readonly DateOnly EffectiveFrom2016 = new(2019, 1, 1);
    public static readonly DateOnly EffectiveTo2016 = new(2025, 6, 30);

    public const decimal PensionEmployeeRate = 0.07m;
    public const decimal PensionEmployerRate = 0.11m;

    public static IReadOnlyList<IncomeTaxCalculator.Bracket> MonthlyIncomeTaxBrackets2025 { get; } =
    [
        new(0m,       2000m,   0.00m, 0m),
        new(2000m,    4000m,   0.15m, 300m),
        new(4000m,    7000m,   0.20m, 500m),
        new(7000m,    10000m,  0.25m, 850m),
        new(10000m,   14000m,  0.30m, 1350m),
        new(14000m,   null,    0.35m, 2050m)
    ];

    public static IReadOnlyList<IncomeTaxCalculator.Bracket> MonthlyIncomeTaxBrackets2016 { get; } =
    [
        new(0m,       600m,    0.00m, 0m),
        new(600m,     1650m,   0.10m, 60m),
        new(1650m,    3200m,   0.15m, 135m),
        new(3200m,    5250m,   0.20m, 295m),
        new(5250m,    7800m,   0.25m, 555m),
        new(7800m,    10900m,  0.30m, 1125m),
        new(10900m,   null,    0.35m, 1675m)
    ];

    /// <summary>Taxable employment income = gross taxable earnings − pension employee contribution.</summary>
    public static decimal TaxableIncome(decimal grossTaxableEarnings, decimal pensionEmployeeContribution) =>
        Math.Max(0m, grossTaxableEarnings - pensionEmployeeContribution);
}
