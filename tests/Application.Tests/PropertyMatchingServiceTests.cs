using Application.DTOs.PropertyMatching;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Tests;

public class PropertyMatchingServiceTests
{
    [Fact]
    public async Task FindMatches_ShouldFilterByDesiredTypes()
    {
        var requirement = CreateRequirement(desiredTypes: [PropertyType.House]);
        var apartment = CreateProperty(type: PropertyType.Apartment);
        var house = CreateProperty(type: PropertyType.House);

        var service = CreateService(requirement, [apartment, house]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        var matched = Assert.Single(result);
        Assert.Equal(house.Id, matched.PropertyId);
    }

    [Fact]
    public async Task FindMatches_ShouldFilterByPriceAreaAndAddress()
    {
        var requirement = CreateRequirement(
            minPrice: 100_000m,
            maxPrice: 180_000m,
            minArea: 50,
            maxArea: 65,
            addressQuery: "Stefan");

        var valid = CreateProperty(price: 150_000m, area: 60, address: "Str. Stefan cel Mare 10");
        var expensive = CreateProperty(price: 250_000m, area: 60, address: "Str. Stefan cel Mare 11");
        var tooLarge = CreateProperty(price: 140_000m, area: 90, address: "Str. Stefan cel Mare 12");
        var wrongAddress = CreateProperty(price: 140_000m, area: 60, address: "Str. Pushkin 1");

        var service = CreateService(requirement, [valid, expensive, tooLarge, wrongAddress]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        var matched = Assert.Single(result);
        Assert.Equal(valid.Id, matched.PropertyId);
    }

    [Fact]
    public async Task FindMatches_ShouldFilterByRadius_WhenIgnoreAreaFalse()
    {
        var requirement = CreateRequirement(searchRadiusMeters: 1_000, ignoreArea: false);
        var near = CreateProperty(latitude: 47.0120, longitude: 28.8650);
        var far = CreateProperty(latitude: 47.3500, longitude: 28.9500);

        var service = CreateService(requirement, [near, far]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        var matched = Assert.Single(result);
        Assert.Equal(near.Id, matched.PropertyId);
    }

    [Fact]
    public async Task FindMatches_ShouldIgnoreRadius_WhenIgnoreAreaTrue()
    {
        var requirement = CreateRequirement(searchRadiusMeters: 100, ignoreArea: true);
        var far = CreateProperty(latitude: 47.3500, longitude: 28.9500);

        var service = CreateService(requirement, [far]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        Assert.Single(result);
        Assert.Equal(far.Id, result[0].PropertyId);
    }

    [Fact]
    public async Task FindMatches_ShouldRejectCandidate_WhenMustHaveCriterionMismatched()
    {
        var mustHaveDefinition = CreateTextCriterionDefinition("wall_material");
        var requirement = CreateRequirement(
            criteria:
            [
                new ClientRequirementCriterion(
                    mustHaveDefinition.Id,
                    RequirementCriterionPriority.MustHave,
                    value: "brick")
            ]);

        var candidate = CreateProperty();
        candidate.UpsertCriterionValue(mustHaveDefinition, "panel");

        var service = CreateService(requirement, [candidate]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        Assert.Empty(result);
    }

    [Fact]
    public async Task FindMatches_ShouldSetFinalScoreToOne_WhenNoSoftCriteria()
    {
        var requirement = CreateRequirement();
        var candidate = CreateProperty();

        var service = CreateService(requirement, [candidate]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        var matched = Assert.Single(result);
        AssertAlmostEqual(1.0, matched.MatchScore);
        AssertAlmostEqual(1.0, matched.Breakdown.BaseScore);
        AssertAlmostEqual(0.0, matched.Breakdown.CriteriaScore);
    }

    [Fact]
    public async Task FindMatches_ShouldWeightImportantHigherThanNiceToHave()
    {
        var important = CreateTextCriterionDefinition("transport");
        var niceToHave = CreateTextCriterionDefinition("balcony");

        var requirement = CreateRequirement(
            minMatchPercentage: 0.1,
            criteria:
            [
                new ClientRequirementCriterion(important.Id, RequirementCriterionPriority.Important, value: "near"),
                new ClientRequirementCriterion(niceToHave.Id, RequirementCriterionPriority.NiceToHave, value: "open")
            ]);

        var importantOnly = CreateProperty(price: 150_000m, area: 55);
        importantOnly.UpsertCriterionValue(important, "near");

        var niceOnly = CreateProperty(price: 150_000m, area: 55);
        niceOnly.UpsertCriterionValue(niceToHave, "open");

        var service = CreateService(requirement, [niceOnly, importantOnly]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        Assert.Equal(2, result.Count);
        Assert.Equal(importantOnly.Id, result[0].PropertyId);
        Assert.Equal(niceOnly.Id, result[1].PropertyId);
        AssertAlmostEqual(45d / 47d, result[0].MatchScore);
        AssertAlmostEqual(42d / 47d, result[1].MatchScore);
    }

    [Fact]
    public async Task FindMatches_ShouldApplyMinMatchThresholdToSoftScore()
    {
        var important = CreateTextCriterionDefinition("internet");
        var requirement = CreateRequirement(
            minMatchPercentage: 0.9,
            criteria:
            [
                new ClientRequirementCriterion(important.Id, RequirementCriterionPriority.Important, value: "fiber")
            ]);

        var candidate = CreateProperty();
        candidate.UpsertCriterionValue(important, "dsl");

        var service = CreateService(requirement, [candidate]);

        var result = await service.FindMatches(new PropertyMatchingRequest(requirement.Id, 10));

        Assert.Empty(result);
    }

    private static PropertyMatchingService CreateService(
        ClientRequirement requirement,
        List<Property> candidates)
    {
        IPropertyRepository propertyRepository = new FakePropertyRepository(candidates);
        IClientRequirementRepository requirementRepository = new FakeClientRequirementRepository(requirement);
        return new PropertyMatchingService(propertyRepository, requirementRepository);
    }

    private static ClientRequirement CreateRequirement(
        double minMatchPercentage = 0.1,
        IReadOnlyCollection<PropertyType>? desiredTypes = null,
        PropertyType desiredType = PropertyType.Apartment,
        decimal minPrice = 100_000m,
        decimal maxPrice = 200_000m,
        double minArea = 50,
        double? maxArea = null,
        string? addressQuery = null,
        bool ignoreArea = false,
        double searchRadiusMeters = 5_000,
        IEnumerable<ClientRequirementCriterion>? criteria = null)
    {
        return new ClientRequirement(
            clientId: Guid.NewGuid(),
            desiredType: desiredType,
            targetLocation: new Location(47.0105, 28.8638),
            searchRadiusMeters: searchRadiusMeters,
            minPrice: minPrice,
            maxPrice: maxPrice,
            minArea: minArea,
            minMatchPercentage: minMatchPercentage,
            priceWeight: 0.6,
            areaWeight: 0.4,
            criteria: criteria,
            desiredTypes: desiredTypes,
            maxArea: maxArea,
            addressQuery: addressQuery,
            ignoreArea: ignoreArea);
    }

    private static Property CreateProperty(
        decimal price = 150_000m,
        double area = 55,
        PropertyType type = PropertyType.Apartment,
        string address = "Str. Stefan cel Mare 10",
        double latitude = 47.0120,
        double longitude = 28.8650)
    {
        return new Property(
            title: "Test property",
            address: address,
            price: price,
            area: area,
            roomsCount: 2,
            location: new Location(latitude, longitude),
            type: type);
    }

    private static PropertyCriterionDefinition CreateTextCriterionDefinition(string code)
    {
        return new PropertyCriterionDefinition(
            code: $"{code}_{Guid.NewGuid():N}",
            displayName: code,
            valueType: PropertyCriterionValueType.Text);
    }

    private static void AssertAlmostEqual(double expected, double actual, double tolerance = 0.0000001)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
    }

    private sealed class FakePropertyRepository : IPropertyRepository
    {
        private readonly List<Property> _properties;

        public FakePropertyRepository(List<Property> properties)
        {
            _properties = properties;
        }

        public Task<Property?> GetById(Guid id)
        {
            return Task.FromResult(_properties.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<Property>> Get(int limit)
        {
            return Task.FromResult(_properties.Take(limit).ToList());
        }

        public Task<Property> Add(Property entity)
        {
            _properties.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Property entity)
        {
        }

        public bool Delete(Property entity)
        {
            return _properties.Remove(entity);
        }

        public Task<List<Property>> GetAvailable()
        {
            var available = _properties
                .Where(x => x.Status == PropertyStatus.Available)
                .ToList();

            return Task.FromResult(available);
        }

        public Task<List<Property>> GetByResponsibleRealtor(Guid realtorId, DateTime fromUtc)
        {
            var items = _properties
                .Where(x => x.ResponsibleRealtorId == realtorId)
                .Where(x => x.CreatedDate >= fromUtc)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<List<Property>> GetByIds(IReadOnlyCollection<Guid> ids)
        {
            var items = _properties.Where(x => ids.Contains(x.Id)).ToList();
            return Task.FromResult(items);
        }

        public Task<Property?> GetByIdForUpdateWithCriteria(Guid id)
        {
            return GetById(id);
        }

        public void AddCriterionValue(PropertyCriterionValue value)
        {
        }

        public Task<List<PropertyMatchCandidate>> GetCandidatesForRequirement(
            ClientRequirement requirement,
            int take)
        {
            var candidates = _properties
                .Take(take)
                .Select(x => new PropertyMatchCandidate(x, 250.0))
                .ToList();

            return Task.FromResult(candidates);
        }
    }

    private sealed class FakeClientRequirementRepository : IClientRequirementRepository
    {
        private readonly ClientRequirement _requirement;

        public FakeClientRequirementRepository(ClientRequirement requirement)
        {
            _requirement = requirement;
        }

        public Task<ClientRequirement?> GetById(Guid id)
        {
            return Task.FromResult(_requirement.Id == id ? _requirement : null);
        }

        public Task<List<ClientRequirement>> Get(int limit)
        {
            return Task.FromResult(new List<ClientRequirement> { _requirement }.Take(limit).ToList());
        }

        public Task<ClientRequirement> Add(ClientRequirement entity)
        {
            return Task.FromResult(entity);
        }

        public void Update(ClientRequirement entity)
        {
        }

        public bool Delete(ClientRequirement entity)
        {
            return true;
        }

        public Task<ClientRequirement?> GetActiveByClient(Guid clientId)
        {
            if (_requirement.ClientId == clientId && _requirement.IsActive)
                return Task.FromResult<ClientRequirement?>(_requirement);

            return Task.FromResult<ClientRequirement?>(null);
        }

        public Task<List<ClientRequirement>> GetActive(int limit)
        {
            var active = _requirement.IsActive
                ? new List<ClientRequirement> { _requirement }.Take(limit).ToList()
                : [];

            return Task.FromResult(active);
        }
    }
}
