namespace Web.Models.Realtors;

public static class RealtorValueDisplay
{
    public static string Level(string? rawLevel)
    {
        return rawLevel?.Trim() switch
        {
            "Junior" => "Junior",
            "Standard" => "Standard",
            "Top" => "Top",
            _ => "Unspecified"
        };
    }
}
