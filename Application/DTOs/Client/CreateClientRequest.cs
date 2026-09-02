namespace Application.DTOs.Client
{
    public record CreateClientRequest(
        string FirstName,
        string LastName,
        string? MiddleName,
        string PhoneNumber,
        string? Email);
}
