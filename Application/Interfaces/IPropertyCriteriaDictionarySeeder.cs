using Application.Seeding;

namespace Application.Interfaces;

public interface IPropertyCriteriaDictionarySeeder
{
    Task<PropertyCriteriaSeedResult> SeedAsync(CancellationToken cancellationToken = default);
}
