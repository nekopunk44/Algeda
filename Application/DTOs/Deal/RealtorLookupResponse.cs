namespace Application.DTOs.Deal
{
    public record RealtorLookupResponse(
        Guid RealtorId,
        string FullName,
        string PhoneNumber,
        string? Email);
}
