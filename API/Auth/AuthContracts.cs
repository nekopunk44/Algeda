using System.ComponentModel.DataAnnotations;

namespace API.Auth
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        public string Password { get; set; } = string.Empty;
    }

    public record LoginResponse(
        string AccessToken,
        DateTime ExpiresAtUtc,
        string TokenType,
        IReadOnlyCollection<string> Roles);

    public record AccountStatusResponse(bool IsFrozen);

    public class ResendEmailConfirmationRequest
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите код.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен состоять из 6 цифр.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите новый пароль.")]
        [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Подтвердите пароль.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ConfirmEmailRequest
    {
        [Required(ErrorMessage = "Введите email.")]
        [EmailAddress(ErrorMessage = "Введите корректный email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите код.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен состоять из 6 цифр.")]
        public string Code { get; set; } = string.Empty;
    }

    public record AuthOperationResponse(string Message);
}
