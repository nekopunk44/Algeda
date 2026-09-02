using Application.DTOs.Property;
using Domain.Enums;

namespace Application.Tests;

public sealed class PropertyResponseTests
{
    [Fact]
    public void WithoutPrivateOwnerData_RemovesOwnerAndInternalAssignmentFields()
    {
        var response = new PropertyResponse(
            Guid.NewGuid(),
            "Дом",
            "Кишинёв",
            100_000m,
            100_000m,
            "USD",
            120,
            4,
            47.01,
            28.86,
            PropertyType.House,
            PropertyStatus.Available,
            null,
            "Иван Иванов",
            "owner@example.com",
            "+37360000000",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "/uploads/properties/example.jpg",
            ["/uploads/properties/example.jpg"],
            [],
            DateTime.UtcNow);

        var publicResponse = response.WithoutPrivateOwnerData();

        Assert.Null(publicResponse.OwnerFullName);
        Assert.Null(publicResponse.OwnerEmail);
        Assert.Null(publicResponse.OwnerPhoneNumber);
        Assert.Null(publicResponse.OwnerClientId);
        Assert.Null(publicResponse.ResponsibleRealtorId);
        Assert.Equal(response.Id, publicResponse.Id);
        Assert.Equal(response.Title, publicResponse.Title);
    }
}
