using Domain.Primitives;

namespace Application.DTOs.Complaint
{
    public record CreateComplaintRequest
    {
        public Guid ClientId { get; init; }

        public Guid? TargetRealtorId { get; init; }

        public Guid? DealId { get; init; }

        public Guid? PropertyId { get; init; }

        public ComplaintCategory Category { get; init; } = ComplaintCategory.Undefined;

        public string Subject { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public CreateComplaintRequest()
        {
        }

        public CreateComplaintRequest(
            Guid clientId,
            Guid targetRealtorId,
            string subject,
            string description)
        {
            ClientId = clientId;
            TargetRealtorId = targetRealtorId;
            Category = ComplaintCategory.Realtor;
            Subject = subject;
            Description = description;
        }
    }
}
