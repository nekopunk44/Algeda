namespace Application.DTOs.PropertyMatching
{
    public record MatchedPropertyScoreBreakdownResponse(
        double BaseScore,
        double CriteriaScore,
        bool PassedMustHave);
}
