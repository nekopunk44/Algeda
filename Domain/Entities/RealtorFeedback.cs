using Domain.Common;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class RealtorFeedback : BaseEntity
    {
        public Guid DealId { get; private set; }
        public Guid ClientId { get; private set; }
        public Guid ServiceRealtorId { get; private set; }
        public Guid? PropertyId { get; private set; }
        public Guid? PropertyResponsibleRealtorId { get; private set; }
        public int ServiceScore { get; private set; }
        public Rating ServiceRating => new(ServiceScore);
        public FeedbackFormType FormType { get; private set; }
        public int? CommunicationScore { get; private set; }
        public int? ResponsivenessScore { get; private set; }
        public int? ExpertiseScore { get; private set; }
        public int? TitleAccuracyScore { get; private set; }
        public int? CriteriaAccuracyScore { get; private set; }
        public int? DescriptionAccuracyScore { get; private set; }
        public int? PhotosAccuracyScore { get; private set; }

        // Legacy fields kept for backward compatibility with old data.
        public FeedbackAlignment TitleAlignment { get; private set; }
        public FeedbackAlignment CriteriaAlignment { get; private set; }
        public FeedbackAlignment DescriptionAlignment { get; private set; }
        public FeedbackAlignment PhotosAlignment { get; private set; }
        public string? Comment { get; private set; }

        private RealtorFeedback()
        {
            ServiceScore = 1;
            FormType = FeedbackFormType.Undefined;
            TitleAlignment = FeedbackAlignment.Undefined;
            CriteriaAlignment = FeedbackAlignment.Undefined;
            DescriptionAlignment = FeedbackAlignment.Undefined;
            PhotosAlignment = FeedbackAlignment.Undefined;
        }

        public RealtorFeedback(
            Guid dealId,
            Guid clientId,
            Guid serviceRealtorId,
            Guid? propertyId,
            Guid? propertyResponsibleRealtorId,
            int serviceScore,
            FeedbackFormType formType,
            int? communicationScore,
            int? responsivenessScore,
            int? expertiseScore,
            int? titleAccuracyScore,
            int? criteriaAccuracyScore,
            int? descriptionAccuracyScore,
            int? photosAccuracyScore,
            string? comment)
        {
            DealId = dealId;
            ClientId = clientId;
            ServiceRealtorId = serviceRealtorId;
            PropertyId = NormalizeOptionalGuid(propertyId, nameof(propertyId));
            PropertyResponsibleRealtorId = NormalizeOptionalGuid(propertyResponsibleRealtorId, nameof(propertyResponsibleRealtorId));
            ServiceScore = serviceScore;
            FormType = formType;
            CommunicationScore = NormalizeOptionalScore(communicationScore);
            ResponsivenessScore = NormalizeOptionalScore(responsivenessScore);
            ExpertiseScore = NormalizeOptionalScore(expertiseScore);
            TitleAccuracyScore = NormalizeOptionalScore(titleAccuracyScore);
            CriteriaAccuracyScore = NormalizeOptionalScore(criteriaAccuracyScore);
            DescriptionAccuracyScore = NormalizeOptionalScore(descriptionAccuracyScore);
            PhotosAccuracyScore = NormalizeOptionalScore(photosAccuracyScore);
            TitleAlignment = FeedbackAlignment.Undefined;
            CriteriaAlignment = FeedbackAlignment.Undefined;
            DescriptionAlignment = FeedbackAlignment.Undefined;
            PhotosAlignment = FeedbackAlignment.Undefined;
            Comment = NormalizeComment(comment);

            Validate();
        }

        private void Validate()
        {
            if (DealId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(DealId)));

            if (ClientId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ClientId)));

            if (ServiceRealtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ServiceRealtorId)));

            _ = ServiceRating;
            ValidateFormType();
            ValidateOptionalScore(CommunicationScore);
            ValidateOptionalScore(ResponsivenessScore);
            ValidateOptionalScore(ExpertiseScore);

            if (FormType == FeedbackFormType.Purchase)
            {
                ValidateOptionalScore(TitleAccuracyScore);
                ValidateOptionalScore(CriteriaAccuracyScore);
                ValidateOptionalScore(DescriptionAccuracyScore);
                ValidateOptionalScore(PhotosAccuracyScore);

                if (!TitleAccuracyScore.HasValue)
                    throw new DomainException(ValidationMessages.InvalidProperty(nameof(TitleAccuracyScore)));
                if (!CriteriaAccuracyScore.HasValue)
                    throw new DomainException(ValidationMessages.InvalidProperty(nameof(CriteriaAccuracyScore)));
                if (!DescriptionAccuracyScore.HasValue)
                    throw new DomainException(ValidationMessages.InvalidProperty(nameof(DescriptionAccuracyScore)));
                if (!PhotosAccuracyScore.HasValue)
                    throw new DomainException(ValidationMessages.InvalidProperty(nameof(PhotosAccuracyScore)));
            }
            else
            {
                if (TitleAccuracyScore.HasValue
                    || CriteriaAccuracyScore.HasValue
                    || DescriptionAccuracyScore.HasValue
                    || PhotosAccuracyScore.HasValue)
                {
                    throw new DomainException("Оценки точности карточки доступны только для сделки покупки.");
                }
            }
        }

        private static Guid? NormalizeOptionalGuid(Guid? value, string fieldName)
        {
            if (!value.HasValue)
                return null;

            if (value.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(fieldName));

            return value.Value;
        }

        private static string? NormalizeComment(string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return null;

            var normalized = comment.Trim();
            return normalized.Length <= 4000 ? normalized : normalized[..4000];
        }

        private void ValidateFormType()
        {
            if (FormType is not (FeedbackFormType.Purchase or FeedbackFormType.Sale))
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(FormType)));
        }

        private static int? NormalizeOptionalScore(int? score)
        {
            if (!score.HasValue)
                return null;

            return score.Value;
        }

        private static void ValidateOptionalScore(int? score)
        {
            if (!score.HasValue)
                return;

            _ = new Rating(score.Value);
        }
    }
}
