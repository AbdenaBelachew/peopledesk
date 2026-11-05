using PeopleDesk.Domain.Payroll;
using Xunit;

namespace PeopleDesk.UnitTests;

/// <summary>
/// Income tax math — verified against the Ethiopia Proclamation 1395/2025 monthly schedule
/// (0–2,000: 0% | 2,001–4,000: 15% | 4,001–7,000: 20% | 7,001–10,000: 25% | 10,001–14,000: 30% | >14,000: 35%).
/// </summary>
public class IncomeTaxCalculatorTests
{
    private static readonly IncomeTaxCalculator.Bracket[] Et2025 = EthiopiaPayroll.MonthlyIncomeTaxBrackets2025.ToArray();
    private static readonly IncomeTaxCalculator.Bracket[] Et2016 = EthiopiaPayroll.MonthlyIncomeTaxBrackets2016.ToArray();

    [Theory]
    [InlineData(1999.99, 0)]          // tax-free threshold
    [InlineData(2000, 0)]
    [InlineData(2000.01, 0)]          // exactly at boundary: > lower bound, 15% − 300 = 0.02 rounds to 0.00
    [InlineData(2555, 83.25)]         // 2555 × 0.15 − 300
    [InlineData(4000, 300)]           // 4000 × 0.15 − 300
    [InlineData(5000, 500)]           // 5000 × 0.20 − 500
    [InlineData(10000, 1650)]         // 10000 × 0.25 − 850
    [InlineData(12555, 2416.50)]      // 12555 × 0.30 − 1350 (verified in e2e)
    [InlineData(14000, 2850)]         // 14000 × 0.30 − 1350
    [InlineData(14001, 2850.35)]      // 14001 × 0.35 − 2050
    [InlineData(30000, 8450)]         // 30000 × 0.35 − 2050
    public void Et2025_ShortcutMethod_MatchesOfficialSchedule(decimal taxable, decimal expected)
    {
        var tax = IncomeTaxCalculator.Compute(taxable, Et2025);
        Assert.Equal(expected, tax);
    }

    [Theory]
    [InlineData(2555, 83.25)]
    [InlineData(5000, 500)]
    [InlineData(12555, 2416.50)]
    [InlineData(30000, 8450)]
    public void Et2025_MarginalMethod_MatchesShortcutMethod(decimal taxable, decimal expected)
    {
        var tax = IncomeTaxCalculator.ComputeMarginal(taxable, Et2025);
        Assert.Equal(expected, tax);
    }

    [Theory]
    [InlineData(600, 0)]              // ET-2016 legacy schedule
    [InlineData(1000, 40)]            // 1000 × 0.10 − 60
    [InlineData(2000, 165)]           // 2000 × 0.15 − 135
    [InlineData(10000, 1875)]         // 10000 × 0.30 − 1125
    [InlineData(20000, 5325)]         // 20000 × 0.35 − 1675
    public void Et2016_LegacySchedule_IsApplied(decimal taxable, decimal expected)
    {
        var tax = IncomeTaxCalculator.Compute(taxable, Et2016);
        Assert.Equal(expected, tax);
    }

    [Fact]
    public void BelowFirstBracket_PaysNothing()
    {
        Assert.Equal(0m, IncomeTaxCalculator.Compute(1500m, Et2025));
        Assert.Equal(0m, IncomeTaxCalculator.Compute(0m, Et2025));
        Assert.Equal(0m, IncomeTaxCalculator.Compute(-500m, Et2025)); // defensive
    }

    [Fact]
    public void BracketTables_AreCoherent()
    {
        Assert.Empty(IncomeTaxCalculator.Validate(Et2025));
        Assert.Empty(IncomeTaxCalculator.Validate(Et2016));
    }

    [Fact]
    public void InvalidBracketTable_IsDetected()
    {
        var broken = new List<IncomeTaxCalculator.Bracket>
        {
            new(100m, 500m, 0.10m, 0m),          // first bracket doesn't start at 0
            new(400m, 300m, 0.15m, 0m),          // upper < lower
            new(600m, null, 1.5m, 0m)            // rate > 1 + gap between 300 and 600
        };
        var errors = IncomeTaxCalculator.Validate(broken);
        Assert.Equal(4, errors.Count);
        Assert.Contains(errors, e => e.Contains("start at 0"));
        Assert.Contains(errors, e => e.Contains("upper bound"));
        Assert.Contains(errors, e => e.Contains("rate must be"));
        Assert.Contains(errors, e => e.Contains("gap"));
    }

    [Fact]
    public void TaxableIncome_NeverNegative_AfterPension()
    {
        Assert.Equal(0m, EthiopiaPayroll.TaxableIncome(100m, 200m));
        Assert.Equal(13055m, EthiopiaPayroll.TaxableIncome(13500m, 445m));
    }

    [Theory]
    [InlineData(0.07, 13500, 945)]
    [InlineData(0.11, 13500, 1485)]
    public void PensionContribution_Rates_AreVersioned(decimal rate, decimal gross, decimal expected)
    {
        Assert.Equal(expected, IncomeTaxCalculator.Round2(gross * rate));
    }

    [Fact]
    public void Round2_UsesBankersSafeMidpointRounding()
    {
        Assert.Equal(2.47m, IncomeTaxCalculator.Round2(2.465m));   // away from zero
        Assert.Equal(945.00m, IncomeTaxCalculator.Round2(13500m * 0.07m));
    }
}
