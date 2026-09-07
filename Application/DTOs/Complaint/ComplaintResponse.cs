using Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Complaint
{
    public record ComplaintResponse(
        Guid Id,
        Guid ClientId,
        Guid? TargetRealtorId,
        Guid? DealId,
        Guid? PropertyId,
        ComplaintCategory Category,
        string Subject,
        string Description,
        ComplaintStatus Status,
        ComplaintReviewVerdict ModerationVerdict,
        DateTime? ResolvedAt,
        string? AdminResolution,
        DateTime CreatedDate);
}
