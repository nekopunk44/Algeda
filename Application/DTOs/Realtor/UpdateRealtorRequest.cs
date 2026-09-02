namespace Application.DTOs.Realtor
{
    public record UpdateRealtorRequest(
        Guid Id,
        string FirstName,
        string LastName,
        string? MiddleName,
        string PhoneNumber);
}
