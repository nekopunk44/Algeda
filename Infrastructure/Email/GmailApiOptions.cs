namespace Infrastructure.Email;

public sealed class GmailApiOptions
{
    public const string SectionName = "GmailApi";

    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public int TimeoutSeconds { get; set; } = 15;
}
