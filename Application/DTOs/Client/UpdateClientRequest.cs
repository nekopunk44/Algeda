namespace Application.DTOs.Client
{
    public record UpdateClientRequest(
        Guid Id,
        string PhoneNumber);
}
