using Domain.Common;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class Review : BaseEntity
    {
        public Guid DealId { get; private set; }
        public Guid RealtorId { get; private set; }
        public Guid ClientId { get; private set; }
        public Rating Rating { get; private set; }
        public int Score => Rating.Value;
        public string? Comment { get; private set; }

        public Review(Guid dealId, Guid realtorId, Guid clientId, int score, string? comment)
        {
            DealId = dealId;
            RealtorId = realtorId;
            ClientId = clientId;
            Rating = new Rating(score);
            Comment = comment;

            Validate();
        }

        private void Validate()
        {
            if (DealId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(DealId)));

            if (RealtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(RealtorId)));

            if (ClientId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ClientId)));

            if (Rating is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(Rating)));
        }
    }
}
