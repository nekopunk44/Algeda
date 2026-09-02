namespace Application.DTOs.PropertyMatching
{
    public record MatchedPropertyResponse(
        Guid PropertyId,
        string Title,
        string Address,
        decimal Price,
        decimal OriginalPriceAmount,
        string OriginalPriceCurrency,
        double Area,
        int RoomsCount,
        double Latitude,
        double Longitude,
        double DistanceMeters,
        double MatchScore,
        MatchedPropertyScoreBreakdownResponse Breakdown);
}
