using Domain.Common;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class Realtor : BaseEntity
    {
        public FullName FullName { get; private set; }
        public string PhoneNumber { get; private set; }
        public string? AvatarPath { get; private set; }

        // Метрики эффективности
        public double CurrentKpiScore { get; private set; }
        public double AverageRating { get; private set; }
        public int DealsThisMonth { get; private set; }
        public RealtorLevel Level { get; private set; }
        public bool IsLevelManuallyAssigned { get; private set; }

        public Realtor(FullName fullName, string phoneNumber)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;
            CurrentKpiScore = 0;
            AverageRating = 0;
            DealsThisMonth = 0;
            Level = RealtorLevel.Junior; // По умолчанию новичок
            IsLevelManuallyAssigned = false;
            AvatarPath = null;

            Validate();
        }

        public void UpdateProfile(FullName fullName, string phoneNumber)
        {
            FullName = fullName;
            PhoneNumber = phoneNumber;

            Validate();
        }

        public void SetAvatarPath(string path)
        {
            AvatarPath = string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim();
        }

        public void RemoveAvatar()
        {
            AvatarPath = null;
        }

        public void UpdateKpi(double kpiScore, double averageRating, int dealsCount)
        {
            if (kpiScore < 0 || kpiScore > 100)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(kpiScore)));

            if (averageRating < 0 || averageRating > 5)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(averageRating)));

            if (dealsCount < 0)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(dealsCount)));

            CurrentKpiScore = kpiScore;
            AverageRating = averageRating;
            DealsThisMonth = dealsCount;
        }

        public void SetManualLevel(RealtorLevel level)
        {
            if (level is RealtorLevel.Undefined)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(level)));

            Level = level;
            IsLevelManuallyAssigned = true;
        }

        public void EnableAutomaticLevel()
        {
            IsLevelManuallyAssigned = false;
        }

        public void SetAutomaticLevel(RealtorLevel level)
        {
            if (level is RealtorLevel.Undefined)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(level)));

            if (IsLevelManuallyAssigned)
                return;

            Level = level;
        }

        private void Validate()
        {
            if (FullName is null)
                throw new DomainException(ValidationMessages.NotEmpty(nameof(FullName)));

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
