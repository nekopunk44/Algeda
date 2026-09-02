using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public sealed class UserSession : BaseEntity
    {
        private const int MaxDeviceNameLength = 160;
        private const int MaxIpAddressLength = 64;
        private const int MaxUserAgentLength = 512;

        public Guid UserId { get; private set; }
        public string DeviceName { get; private set; } = string.Empty;
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public DateTime LastSeenAtUtc { get; private set; }
        public DateTime ExpiresAtUtc { get; private set; }
        public DateTime? RevokedAtUtc { get; private set; }

        public UserSession(
            Guid id,
            Guid userId,
            string deviceName,
            string? ipAddress,
            string? userAgent,
            DateTime expiresAtUtc)
        {
            Id = id;
            UserId = userId;
            DeviceName = NormalizeRequired(deviceName, MaxDeviceNameLength);
            IpAddress = NormalizeOptional(ipAddress, MaxIpAddressLength);
            UserAgent = NormalizeOptional(userAgent, MaxUserAgentLength);
            LastSeenAtUtc = CreatedDate;
            ExpiresAtUtc = expiresAtUtc;

            Validate();
        }

        private UserSession()
        {
        }

        public bool IsActive(DateTime nowUtc)
        {
            return RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
        }

        public void Touch(DateTime nowUtc)
        {
            LastSeenAtUtc = nowUtc;
        }

        public void Refresh(
            string deviceName,
            string? ipAddress,
            string? userAgent,
            DateTime expiresAtUtc,
            DateTime nowUtc)
        {
            DeviceName = NormalizeRequired(deviceName, MaxDeviceNameLength);
            IpAddress = NormalizeOptional(ipAddress, MaxIpAddressLength);
            UserAgent = NormalizeOptional(userAgent, MaxUserAgentLength);
            ExpiresAtUtc = expiresAtUtc;
            LastSeenAtUtc = nowUtc;
            RevokedAtUtc = null;

            Validate();
        }

        public void Revoke(DateTime nowUtc)
        {
            RevokedAtUtc ??= nowUtc;
        }

        private void Validate()
        {
            if (Id == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(Id)));

            if (UserId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(UserId)));

            if (string.IsNullOrWhiteSpace(DeviceName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(DeviceName)));

            if (ExpiresAtUtc <= CreatedDate)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(ExpiresAtUtc)));
        }

        private static string NormalizeRequired(string value, int maxLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "Неизвестное устройство"
                : value.Trim();

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }
    }
}
