namespace Application.DTOs.Auth
{
    public record RegisterClientRequest(
        string FirstName,
        string LastName,
        string? MiddleName,
        string Email,
        string PhoneNumber,
        string Password,
        string ConfirmPassword);
}
