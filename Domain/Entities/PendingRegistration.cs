using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class PendingRegistration : BaseEntity
    {
        public PendingRegistrationType Type { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string? MiddleName { get; private set; }
        public string Email { get; private set; }
        public string PhoneNumber { get; private set; }
        public string PasswordHash { get; private set; }
        public string CodeHash { get; private set; }
        public DateTime CodeExpiresAtUtc { get; private set; }
        public DateTime LastCodeSentAtUtc { get; private set; }

        public PendingRegistration(
            PendingRegistrationType type,
            string firstName,
            string lastName,
            string? middleName,
            string email,
            string phoneNumber,
            string passwordHash,
            string codeHash,
            DateTime codeExpiresAtUtc,
            DateTime lastCodeSentAtUtc)
        {
            Type = type;
            FirstName = firstName.Trim();
            LastName = lastName.Trim();
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
            Email = email.Trim().ToLowerInvariant();
            PhoneNumber = phoneNumber.Trim();
            PasswordHash = passwordHash;
            CodeHash = codeHash;
            CodeExpiresAtUtc = codeExpiresAtUtc;
            LastCodeSentAtUtc = lastCodeSentAtUtc;

            Validate();
        }

        public void RefreshCode(string codeHash, DateTime expiresAtUtc, DateTime sentAtUtc)
        {
            if (string.IsNullOrWhiteSpace(codeHash))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(codeHash)));

            CodeHash = codeHash;
            CodeExpiresAtUtc = expiresAtUtc;
            LastCodeSentAtUtc = sentAtUtc;
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

            if (string.IsNullOrWhiteSpace(PasswordHash))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(PasswordHash)));

            if (string.IsNullOrWhiteSpace(CodeHash))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(CodeHash)));
        }
    }
}
