using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class RealtorRegistrationRequest : BaseEntity
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string? MiddleName { get; private set; }
        public string Email { get; private set; }
        public string PhoneNumber { get; private set; }
        public Guid? IdentityUserId { get; private set; }
        public RealtorRegistrationRequestStatus Status { get; private set; }
        public string? ReviewComment { get; private set; }
        public DateTime? ReviewedAt { get; private set; }

        public RealtorRegistrationRequest(
            string firstName,
            string lastName,
            string? middleName,
            string email,
            string phoneNumber,
            Guid? identityUserId = null)
        {
            FirstName = firstName;
            LastName = lastName;
            MiddleName = middleName;
            Email = email;
            PhoneNumber = phoneNumber;
            IdentityUserId = identityUserId;
            Status = RealtorRegistrationRequestStatus.Pending;

            Validate();
        }

        public void Approve(string? comment = null)
        {
            EnsurePending();
            Status = RealtorRegistrationRequestStatus.Approved;
            ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            ReviewedAt = DateTime.UtcNow;
        }

        public void Reject(string? reason = null)
        {
            EnsurePending();
            Status = RealtorRegistrationRequestStatus.Rejected;
            ReviewComment = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            ReviewedAt = DateTime.UtcNow;
        }

        public void AttachIdentityUser(Guid identityUserId)
        {
            if (identityUserId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(identityUserId)));

            IdentityUserId = identityUserId;
        }

        public void UpdateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Email)));

            if (email.Length > 256)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Email)));

            Email = email.Trim();
        }

        private void EnsurePending()
        {
            if (Status != RealtorRegistrationRequestStatus.Pending)
            {
                throw new DomainException("Статус заявки уже обработан и не может быть изменен повторно.");
            }
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(FirstName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(FirstName)));

            if (string.IsNullOrWhiteSpace(LastName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(LastName)));

            if (string.IsNullOrWhiteSpace(Email))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Email)));

            if (Email.Length > 256)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Email)));

            if (string.IsNullOrWhiteSpace(PhoneNumber))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(PhoneNumber)));
        }
    }
}
