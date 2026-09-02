namespace Application.Email;

public sealed record EmailMessage(
    IReadOnlyCollection<string> To,
    string Subject,
    string Body,
    bool IsBodyHtml = false);
