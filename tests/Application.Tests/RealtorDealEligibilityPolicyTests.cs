using Application.Options;
using Application.Services;

namespace Application.Tests;

public class RealtorDealEligibilityPolicyTests
{
    [Fact]
    public void Evaluate_ShouldAllow_WhenRestrictionsDisabled()
    {
        var policy = new RealtorDealEligibilityPolicy(CreateOptions());
        var result = policy.Evaluate(
            restrictionsEnabled: false,
            matchedTier: null,
            eligibilitySettings: CreateOptions().Eligibility,
            clientTrustScore: 0,
            adminPerformanceScore: 0,
            criticalComplaintsCount: 0,
            completedDealsHistoryCount: 0,
            realtorLevel: "Junior");

        Assert.True(result.IsEligible);
        Assert.False(result.IsExpensiveDeal);
    }

    [Fact]
    public void Evaluate_ShouldBlock_WhenRealtorLevelIsLowerThanRequiredTier()
    {
        var options = CreateOptions();
        var policy = new RealtorDealEligibilityPolicy(options);
        var tier = options.Eligibility.PriceTiers.Single();

        var result = policy.Evaluate(
            restrictionsEnabled: true,
            matchedTier: tier,
            eligibilitySettings: options.Eligibility,
            clientTrustScore: 0,
            adminPerformanceScore: 0,
            criticalComplaintsCount: 0,
            completedDealsHistoryCount: 0,
            realtorLevel: "Junior");

        Assert.False(result.IsEligible);
        Assert.Equal("LEVEL_PRICE_RANGE", result.BlockReasonCode);
    }

    [Fact]
    public void Evaluate_ShouldAllow_WhenRealtorLevelMatchesRequiredTier()
    {
        var options = CreateOptions();
        var policy = new RealtorDealEligibilityPolicy(options);
        var tier = options.Eligibility.PriceTiers.Single();

        var result = policy.Evaluate(
            restrictionsEnabled: true,
            matchedTier: tier,
            eligibilitySettings: options.Eligibility,
            clientTrustScore: 0,
            adminPerformanceScore: 0,
            criticalComplaintsCount: 0,
            completedDealsHistoryCount: 0,
            realtorLevel: "Top");

        Assert.True(result.IsEligible);
        Assert.Null(result.BlockReasonCode);
    }

    [Fact]
    public void Evaluate_ShouldAllow_WhenRealtorLevelIsHigherThanRequiredTier()
    {
        var options = CreateOptions("Junior");
        var policy = new RealtorDealEligibilityPolicy(options);
        var tier = options.Eligibility.PriceTiers.Single();

        var result = policy.Evaluate(
            restrictionsEnabled: true,
            matchedTier: tier,
            eligibilitySettings: options.Eligibility,
            clientTrustScore: 0,
            adminPerformanceScore: 0,
            criticalComplaintsCount: 0,
            completedDealsHistoryCount: 0,
            realtorLevel: "Top");

        Assert.True(result.IsEligible);
        Assert.Null(result.BlockReasonCode);
    }

    private static RealtorEfficiencyOptions CreateOptions(string tierName = "Top")
    {
        return new RealtorEfficiencyOptions
        {
            Eligibility = new EligibilityOptions
            {
                RestrictionsEnabled = true,
                PriceTiers =
                [
                    new EligibilityTierOptions
                    {
                        Name = tierName,
                        MinPrice = 0m,
                        MaxPrice = null,
                        SortOrder = 0
                    }
                ]
            }
        };
    }
}
