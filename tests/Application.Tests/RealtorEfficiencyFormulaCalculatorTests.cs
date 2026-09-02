using Application.Options;
using Application.Services;

namespace Application.Tests;

public class RealtorEfficiencyFormulaCalculatorTests
{
    [Fact]
    public void Calculate_ShouldReturn5_WhenAllComponentsAreMaxed()
    {
        var options = CreateDefaultOptions();
        var calculator = new RealtorEfficiencyFormulaCalculator(options);

        var result = calculator.Calculate(new RealtorScoreCalculationInput(
            ServiceFeedbackCount: 10,
            AverageServiceRating: 5.0,
            PropertyFeedbackCount: 10,
            AveragePropertyAccuracyScore: 5,
            ConfirmedComplaintCount: 0,
            PartiallyConfirmedComplaintCount: 0,
            CriticalComplaintCount: 0,
            ManagedPropertyCount: 10,
            AveragePropertyDataQualityScore: 5,
            ActivityEventCount: 10,
            TotalActivityPoints: 100,
            TotalDealsInWindow: 10,
            CompletedDealsInWindow: 10,
            CancelledDealsInWindow: 0,
            TotalCommissionInWindow: 100000m));

        Assert.Equal(5d, result.ClientTrustScore, precision: 6);
        Assert.Equal(5d, result.AdminPerformanceScore, precision: 6);
    }

    [Fact]
    public void Calculate_ShouldReturn0_WhenNoDataProvided()
    {
        var options = CreateDefaultOptions();
        var calculator = new RealtorEfficiencyFormulaCalculator(options);

        var result = calculator.Calculate(new RealtorScoreCalculationInput(
            ServiceFeedbackCount: 0,
            AverageServiceRating: 0,
            PropertyFeedbackCount: 0,
            AveragePropertyAccuracyScore: 0,
            ConfirmedComplaintCount: 0,
            PartiallyConfirmedComplaintCount: 0,
            CriticalComplaintCount: 0,
            ManagedPropertyCount: 0,
            AveragePropertyDataQualityScore: 0,
            ActivityEventCount: 0,
            TotalActivityPoints: 0,
            TotalDealsInWindow: 0,
            CompletedDealsInWindow: 0,
            CancelledDealsInWindow: 0,
            TotalCommissionInWindow: 0m));

        Assert.Equal(0d, result.ClientTrustScore, precision: 6);
        Assert.Equal(0d, result.AdminPerformanceScore, precision: 6);
    }

    [Fact]
    public void Calculate_ShouldApplyComplaintPenaltyForCts_WhenServiceFeedbackExists()
    {
        var options = CreateDefaultOptions();
        var calculator = new RealtorEfficiencyFormulaCalculator(options);

        var result = calculator.Calculate(new RealtorScoreCalculationInput(
            ServiceFeedbackCount: 5,
            AverageServiceRating: 4.0,
            PropertyFeedbackCount: 0,
            AveragePropertyAccuracyScore: 0,
            ConfirmedComplaintCount: 2,
            PartiallyConfirmedComplaintCount: 1,
            CriticalComplaintCount: 1,
            ManagedPropertyCount: 0,
            AveragePropertyDataQualityScore: 0,
            ActivityEventCount: 0,
            TotalActivityPoints: 0,
            TotalDealsInWindow: 0,
            CompletedDealsInWindow: 0,
            CancelledDealsInWindow: 0,
            TotalCommissionInWindow: 0m));

        Assert.InRange(result.ComplaintPenaltyComponent, 0d, 5d);
        Assert.InRange(result.ClientTrustScore, 0d, 5d);
    }

    private static RealtorEfficiencyOptions CreateDefaultOptions()
    {
        return new RealtorEfficiencyOptions();
    }
}
