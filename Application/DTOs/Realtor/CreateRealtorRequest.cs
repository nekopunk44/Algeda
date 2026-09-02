namespace Application.DTOs.Realtor
{
    public record CreateRealtorRequest(
        string FirstName,
        string LastName,
        string? MiddleName,
        string PhoneNumber);
}
