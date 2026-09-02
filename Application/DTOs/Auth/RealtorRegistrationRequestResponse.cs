namespace Application.DTOs.Auth
{
    public record RealtorRegistrationRequestResponse(
        Guid Id,
        string FirstName,
        string LastName,
        string? MiddleName,
        string Email,
        string PhoneNumber,
        Guid? IdentityUserId,
        string Status,
        string? ReviewComment,
        DateTime? ReviewedAt,
        DateTime CreatedDate);
}
