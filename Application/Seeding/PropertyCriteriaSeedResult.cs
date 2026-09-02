namespace Application.Seeding;

public sealed record PropertyCriteriaSeedResult(
    int TotalDefinitionsInDictionary,
    int TotalOptionsInDictionary,
    int CreatedDefinitions,
    int UpdatedDefinitions);
