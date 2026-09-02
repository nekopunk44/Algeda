namespace Web.Models.Realtors;

public sealed class RealtorCardViewModel
{
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? MiddleName { get; init; }

    public string PhoneNumber { get; init; } = string.Empty;

    public string? Email { get; init; }
    public string? AvatarPath { get; init; }

    public double AverageRating { get; init; }

    public string Level { get; init; } = "Undefined";

    public bool IsLevelManuallyAssigned { get; init; }

    public int DealsThisMonth { get; init; }

    public string FullName
    {
        get
        {
            var parts = new[] { LastName, FirstName, MiddleName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim());

            return string.Join(" ", parts);
        }
    }
}
