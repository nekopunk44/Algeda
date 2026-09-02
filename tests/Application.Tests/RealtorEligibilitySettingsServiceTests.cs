using Application.Options;
using Application.Services;
using Domain.Entities;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Application.Tests;

public class RealtorEligibilitySettingsServiceTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultTiers_WhenStartupRangesOverlap()
    {
        var options = new RealtorEfficiencyOptions
        {
            Eligibility = new EligibilityOptions
            {
                PriceTiers =
                [
                    new EligibilityTierOptions { Name = "Junior", MinPrice = 0m, MaxPrice = 500000m, SortOrder = 0 },
                    new EligibilityTierOptions { Name = "Standard", MinPrice = 0m, MaxPrice = 400000m, SortOrder = 1 },
                    new EligibilityTierOptions { Name = "Top", MinPrice = 0m, MaxPrice = 1000000m, SortOrder = 2 }
                ]
            }
        };

        var service = new RealtorEligibilitySettingsService(
            OptionsFactory.Create(options),
            new MemorySystemSettingRepository());
        var settings = service.GetCurrent();

        Assert.Equal(3, settings.PriceTiers.Count);
        Assert.Equal(0m, settings.PriceTiers[0].MinPrice);
        Assert.Equal(299999.99m, settings.PriceTiers[0].MaxPrice);
        Assert.Equal(0m, settings.PriceTiers[1].MinPrice);
        Assert.Equal(599999.99m, settings.PriceTiers[1].MaxPrice);
        Assert.Equal(0m, settings.PriceTiers[2].MinPrice);
        Assert.Null(settings.PriceTiers[2].MaxPrice);
    }

    [Fact]
    public void UpdateFromAdmin_ShouldPersistSettings()
    {
        var repository = new MemorySystemSettingRepository();
        var options = new RealtorEfficiencyOptions();
        var service = new RealtorEligibilitySettingsService(OptionsFactory.Create(options), repository);

        service.UpdateFromAdmin(new(
            RestrictionsEnabled: true,
            MinConfirmedHistoryDeals: 0,
            CriticalComplaintLookbackDays: 365,
            ScoreSnapshotMaxAgeHours: 24,
            BlockOnCriticalComplaints: false,
            PriceTiers:
            [
                new("Junior", 0m, 250000m, 0, 0, 0),
                new("Standard", 0m, 700000m, 0, 0, 1),
                new("Top", 0m, null, 0, 0, 2)
            ]));

        var restoredService = new RealtorEligibilitySettingsService(OptionsFactory.Create(options), repository);
        var restored = restoredService.GetCurrent();

        Assert.Equal(250000m, restored.PriceTiers[0].MaxPrice);
        Assert.Equal(700000m, restored.PriceTiers[1].MaxPrice);
        Assert.Null(restored.PriceTiers[2].MaxPrice);
    }

    private sealed class MemorySystemSettingRepository : Application.Interfaces.ISystemSettingRepository
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string? GetValue(string key)
        {
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        public void Upsert(string key, string value)
        {
            _values[key] = value;
        }

        public Task<SystemSetting?> GetById(Guid id) => Task.FromResult<SystemSetting?>(null);

        public Task<List<SystemSetting>> Get(int limit) => Task.FromResult(new List<SystemSetting>());

        public Task<SystemSetting> Add(SystemSetting entity) => Task.FromResult(entity);

        public void Update(SystemSetting entity)
        {
        }

        public bool Delete(SystemSetting entity) => true;
    }
}
