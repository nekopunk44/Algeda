using Domain.Primitives;

namespace Application.DTOs.RealtorKpi
{
    public record RealtorKpiResponse(
        Guid RealtorId,
        double FinalScore,
        double QuantitativeScore,
        double QualitativeScore,
        double DisciplinaryScore,
        double AverageRating,
        int DealsCount,
        RealtorLevel Level);
}
