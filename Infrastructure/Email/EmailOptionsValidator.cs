using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var errors = new List<string>();

        if (options.Enabled)
        {
            if (!IsValidEmail(options.FromAddress))
            {
                errors.Add("Email:FromAddress must be a valid email address when Email:Enabled = true.");
            }

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add("Email:Host is required when Email:Enabled = true.");
            }

            if (options.Port is < 1 or > 65535)
            {
                errors.Add("Email:Port must be in range 1..65535.");
            }

            if (string.IsNullOrWhiteSpace(options.User))
            {
                errors.Add("Email:User is required when Email:Enabled = true.");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                errors.Add("Email:Password is required when Email:Enabled = true.");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out _);
    }
}
