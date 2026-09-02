using Domain.Enums;

namespace Application.DTOs.Deal
{
    public record DealWorkflowResponse(
        Guid Id,
        Guid ClientId,
        string ClientFullName,
        string ClientPhoneNumber,
        string? ClientEmail,
        Guid? PropertyId,
        string? PropertyTitle,
        Guid? ClientRequirementId,
        DealSource Source,
        DealStatus Status,
        bool IsIncoming,
        Guid? RealtorId,
        string? RealtorFullName,
        string? RealtorPhoneNumber,
        string? RealtorEmail,
        string? RequestMessage,
        DateTime? AcceptedAtUtc,
        DateTime? RejectedAtUtc,
        Guid? PriorityRealtorId,
        DateTime? PriorityUntilUtc,
        DateTime? CompletedAtUtc,
        IReadOnlyList<DealNoteResponse> Notes,
        DateTime CreatedDate);
}
