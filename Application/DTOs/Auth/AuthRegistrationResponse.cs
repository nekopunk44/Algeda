namespace Application.DTOs.Auth
{
    public record AuthRegistrationResponse(
        string Message,
        string RegistrationType,
        string Status);
}
