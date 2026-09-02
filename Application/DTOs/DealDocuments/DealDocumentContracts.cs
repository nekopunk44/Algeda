using Domain.Entities;
using Domain.Primitives;

namespace Application.DTOs.DealDocuments
{
    public record CreateDealDocumentRequest(
        string Title,
        string FileName,
        string ContentType,
        byte[] Content);

    public record DealDocumentResponse(
        Guid Id,
        Guid DealId,
        string Title,
        string OriginalFileName,
        string ContentType,
        long FileSizeBytes,
        Guid? UploadedByUserId,
        string UploadedByDisplayName,
        string? UploadedByEmail,
        DateTime CreatedDate);

    public record DealDocumentFileResponse(
        Guid Id,
        string Title,
        string OriginalFileName,
        string ContentType,
        byte[] Content);

    public record DealDocumentActorContext(
        Guid? UserId,
        string? Email,
        string? DisplayName,
        bool IsAdmin,
        bool IsRealtor,
        string? IpAddress,
        string? UserAgent);

    public record DealDocumentAccessLogFilter(
        DateOnly? DateFrom,
        DateOnly? DateTo,
        DealDocumentAction? Action,
        Guid? DealId,
        string? Search,
        int Limit);

    public record DealDocumentAccessLogResponse(
        Guid Id,
        Guid DealId,
        Guid DealDocumentId,
        string DocumentTitle,
        string DocumentFileName,
        DealDocumentAction Action,
        Guid? ActorUserId,
        string ActorDisplayName,
        string? ActorEmail,
        string ActorRole,
        string? IpAddress,
        string? UserAgent,
        DateTime CreatedDate,
        string DealSummary,
        string ClientSummary,
        string RealtorSummary);

    public record DealDocumentAccessLogSearchRow(
        DealDocumentAccessLog Log,
        string DealSummary,
        string ClientSummary,
        string RealtorSummary);
}
