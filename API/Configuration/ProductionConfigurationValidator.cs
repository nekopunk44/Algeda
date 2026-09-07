using Infrastructure.Email;

namespace API.Configuration;

public static class ProductionConfigurationValidator
{
    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        bool isOperationalCommand)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return;
        }

        if (isOperationalCommand)
        {
            return;
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts)
            || allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(host => host == "*"))
        {
            throw new InvalidOperationException(
                "AllowedHosts должен содержать точные production-хосты, а не '*'.");
        }

        var proxyEnabled = configuration.GetValue<bool>($"{ReverseProxyOptions.SectionName}:Enabled");
        if (!proxyEnabled)
        {
            throw new InvalidOperationException(
                "В production необходимо включить ReverseProxy:Enabled и настроить доверенные proxy/network.");
        }

        var emailEnabled = configuration.GetValue<bool>($"{EmailOptions.SectionName}:Enabled");
        var gmailEnabled = configuration.GetValue<bool>($"{GmailApiOptions.SectionName}:Enabled");
        if (!emailEnabled && !gmailEnabled)
        {
            throw new InvalidOperationException(
                "В production необходимо включить SMTP (Email:Enabled) или Gmail API (GmailApi:Enabled). " +
                "Без почтового канала регистрация и восстановление пароля не работают.");
        }

        if (string.IsNullOrWhiteSpace(
                configuration[$"{AutoMapperLicenseOptions.SectionName}:LicenseKey"]))
        {
            throw new InvalidOperationException(
                "В production необходима лицензия AutoMapper. Укажите AutoMapper__LicenseKey через secret.");
        }
    }
}
