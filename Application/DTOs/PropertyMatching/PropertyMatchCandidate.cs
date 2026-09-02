using DomainProperty = Domain.Entities.Property;

namespace Application.DTOs.PropertyMatching
{
    public sealed record PropertyMatchCandidate(
        DomainProperty Property,
        double DistanceMeters);
}
