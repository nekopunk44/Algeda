using Application.DTOs.RealtorEfficiency;
using Application.Options;
using Application.Services;
using Domain.Primitives;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Application.Tests;

public sealed class RealtorSettingsPersistenceTests
{
    [Fact]
    public void CommissionSettings_ShouldLoadPersistedValuesInNewScope()
    {
        var repository = new InMemorySystemSettingRepository();
        var options = OptionsFactory.Create(new RealtorCommissionOptions());
        var firstScope = new RealtorCommissionSettingsService(options, repository);

        firstScope.UpdateFromAdmin(new UpdateRealtorCommissionSettingsRequest(
        [
            new UpdateRealtorLevelCommissionRequest(RealtorLevel.Junior, 22.25m),
            new UpdateRealtorLevelCommissionRequest(RealtorLevel.Standard, 33.50m),
            new UpdateRealtorLevelCommissionRequest(RealtorLevel.Top, 44.75m)
        ]));

        var nextScope = new RealtorCommissionSettingsService(options, repository);

        Assert.Equal(22.25m, nextScope.GetPercentForLevel(RealtorLevel.Junior));
        Assert.Equal(33.50m, nextScope.GetPercentForLevel(RealtorLevel.Standard));
        Assert.Equal(44.75m, nextScope.GetPercentForLevel(RealtorLevel.Top));
    }

    [Fact]
    public void LevelSettings_ShouldLoadPersistedValuesInNewScope()
    {
        var repository = new InMemorySystemSettingRepository();
        var options = OptionsFactory.Create(new RealtorEfficiencyOptions());
        var firstScope = new RealtorLevelSettingsService(options, repository);

        firstScope.UpdateFromAdmin(new UpdateRealtorLevelSettingsRequest(
            DemotionBuffer: 0.45,
            Rules:
            [
                new UpdateRealtorLevelRuleRequest(RealtorLevel.Junior, 0, 0, 0, 0),
                new UpdateRealtorLevelRuleRequest(RealtorLevel.Standard, 12, 3.60, 3.70, 1),
                new UpdateRealtorLevelRuleRequest(RealtorLevel.Top, 35, 4.40, 4.30, 2)
            ]));

        var nextScope = new RealtorLevelSettingsService(options, repository);
        var restored = nextScope.GetCurrent();

        Assert.Equal(0.45, restored.DemotionBuffer);
        var standard = Assert.Single(restored.Rules, x => x.Level == RealtorLevel.Standard);
        Assert.Equal(12, standard.MinCompletedDeals);
        Assert.Equal(3.60, standard.MinClientTrustScore);
        Assert.Equal(3.70, standard.MinAdminPerformanceScore);
        var top = Assert.Single(restored.Rules, x => x.Level == RealtorLevel.Top);
        Assert.Equal(35, top.MinCompletedDeals);
        Assert.Equal(4.40, top.MinClientTrustScore);
        Assert.Equal(4.30, top.MinAdminPerformanceScore);
    }
}
