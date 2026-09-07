using Application.DTOs.Client;
using Application.DTOs.ClientRequirement;
using Application.DTOs.PropertyMatching;
using Application.Validators;
using Domain.Enums;

namespace Application.Tests;

public class RequestValidatorsTests
{
    [Fact]
    public void CreateRequirementRequest_ShouldPass_WhenRequestIsValid()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest();

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateRequirementRequest_ShouldPass_WhenDesiredTypesAreEmpty()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            DesiredType = PropertyType.Undefined,
            DesiredTypes = []
        };

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateRequirementRequest_ShouldFail_WhenDesiredTypesContainDuplicates()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            DesiredTypes = [PropertyType.Apartment, PropertyType.Apartment]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Типы недвижимости"));
    }

    [Fact]
    public void CreateRequirementRequest_ShouldFail_WhenDesiredTypesContainUndefined()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            DesiredTypes = [PropertyType.Undefined]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Типы недвижимости"));
    }

    [Fact]
    public void CreateRequirementRequest_ShouldFail_WhenMaxAreaLessThanMinArea()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            MinArea = 60,
            MaxArea = 50
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Максимальная площадь"));
    }

    [Fact]
    public void CreateRequirementRequest_ShouldFail_WhenWeightsDoNotSumToOne()
    {
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            PriceWeight = 0.7,
            AreaWeight = 0.4
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Сумма весов"));
    }

    [Fact]
    public void CreateRequirementRequest_ShouldFail_WhenCriteriaContainsDuplicateDefinitionId()
    {
        var duplicatedCriterionId = Guid.NewGuid();
        var validator = new CreateRequirementRequestValidator();
        var request = CreateValidRequirementRequest() with
        {
            Criteria =
            [
                new RequirementCriterionRequest(
                    duplicatedCriterionId,
                    RequirementCriterionPriority.Important,
                    "value-1"),
                new RequirementCriterionRequest(
                    duplicatedCriterionId,
                    RequirementCriterionPriority.NiceToHave,
                    "value-2")
            ]
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage.Contains("Критерии подбора"));
    }

    [Fact]
    public void CreateClientRequest_ShouldFail_WhenPhoneNumberInvalid()
    {
        var validator = new CreateClientRequestValidator();
        var request = new CreateClientRequest(
            "Ivan",
            "Client",
            null,
            "invalid_phone",
            "client@example.com");

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(CreateClientRequest.PhoneNumber));
    }

    [Fact]
    public void PropertyMatchingRequest_ShouldFail_WhenLimitOutOfRange()
    {
        var validator = new PropertyMatchingRequestValidator();
        var request = new PropertyMatchingRequest(Guid.NewGuid(), 0);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(PropertyMatchingRequest.Limit));
    }

    private static CreateRequirementRequest CreateValidRequirementRequest()
    {
        return new CreateRequirementRequest(
            ClientId: Guid.NewGuid(),
            DesiredType: PropertyType.Apartment,
            DesiredTypes: [PropertyType.Apartment],
            Latitude: 47.0105,
            Longitude: 28.8638,
            SearchRadiusMeters: 5_000,
            IgnoreArea: false,
            MinPrice: 80_000,
            MaxPrice: 160_000,
            MinArea: 45,
            MaxArea: null,
            AddressQuery: null,
            MinMatchPercentage: 0.6,
            PriceWeight: 0.6,
            AreaWeight: 0.4,
            Criteria:
            [
                new RequirementCriterionRequest(
                    Guid.NewGuid(),
                    RequirementCriterionPriority.Important,
                    "central")
            ]);
    }
}
