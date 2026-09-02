using Domain.Primitives;

namespace Application.DTOs.Complaint
{
    public record ResolveComplaintRequest(
        Guid ComplaintId,
        ComplaintReviewVerdict Verdict,
        string Resolution);
}
