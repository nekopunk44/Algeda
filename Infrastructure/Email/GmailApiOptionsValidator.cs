using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public sealed class GmailApiOptionsValidator : IValidateOptions<GmailApiOptions>
{
    public ValidateOptionsResult Validate(string? name, GmailApiOptions options)
    {
        var errors = new List<string>();

        if (options.TimeoutSeconds is < 1 or > 120)
        {
            errors.Add("GmailApi:TimeoutSeconds must be in range 1..120.");
        }

        if (!options.Enabled)
        {
            return errors.Count > 0
                ? ValidateOptionsResult.Fail(errors)
                : ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            errors.Add("GmailApi:ClientId is required when GmailApi:Enabled = true.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            errors.Add("GmailApi:ClientSecret is required when GmailApi:Enabled = true.");
        }

        if (string.IsNullOrWhiteSpace(options.RefreshToken))
        {
            errors.Add("GmailApi:RefreshToken is required when GmailApi:Enabled = true.");
        }

        if (!IsValidEmail(options.FromAddress))
        {
            errors.Add("GmailApi:FromAddress must be a valid email address.");
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
