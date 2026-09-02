using Domain.Common;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class Client : BaseEntity
    {
        public FullName FullName { get; private set; }
        public string PhoneNumber { get; private set; }
        public string? Email { get; private set; }

        public Client(FullName fullName, string phoneNumber, string? email)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;
            Email = email;

            Validate();
        }

        public void Update(string newPhone)
        {
            PhoneNumber = newPhone;
            Validate();
        }

        public void UpdateProfile(FullName fullName, string phoneNumber, string? email)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;
            Email = string.IsNullOrWhiteSpace(email)
                ? null
                : email.Trim();

            Validate();
        }

        private void Validate()
        {
            if (FullName is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(FullName)));

            if (PhoneNumber is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(PhoneNumber)));

            if (string.IsNullOrWhiteSpace(PhoneNumber))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(PhoneNumber)));

            if (!IsPhoneNumberValid(PhoneNumber))
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(PhoneNumber)));
        }

        private static bool IsPhoneNumberValid(string value)
        {
            var phone = value.Trim();
            var plusCount = phone.Count(c => c == '+');
            if (plusCount > 1 || (plusCount == 1 && !phone.StartsWith("+", StringComparison.Ordinal)))
                return false;

            if (phone.Any(c => !char.IsDigit(c) && c != '+' && c != ' ' && c != '-' && c != '(' && c != ')'))
                return false;

            var digitCount = phone.Count(char.IsDigit);
            return digitCount is >= 7 and <= 15;
        }
    }
}
