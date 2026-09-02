namespace Application.DTOs.Auth
{
    public record RegisterRealtorRequest(
        string FirstName,
        string LastName,
        string? MiddleName,
        string Email,
        string PhoneNumber,
        string Password,
        string ConfirmPassword);
}
