namespace Application.DTOs.Deal
{
    public record DealNoteResponse(
        Guid Id,
        Guid? AuthorRealtorId,
        string Text,
        DateTime? UpdatedAtUtc,
        DateTime CreatedDate);
}
