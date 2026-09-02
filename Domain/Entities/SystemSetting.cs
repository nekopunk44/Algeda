using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class SystemSetting : BaseEntity
    {
        public string Key { get; private set; }

        public string Value { get; private set; }

        public DateTime UpdatedAtUtc { get; private set; }

        public SystemSetting(string key, string value)
        {
            Key = NormalizeKey(key);
            Value = value ?? throw new DomainException(ValidationMessages.NotEmpty(nameof(Value)));
            UpdatedAtUtc = DateTime.UtcNow;
        }

        private SystemSetting()
        {
            Key = string.Empty;
            Value = string.Empty;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void UpdateValue(string value)
        {
            Value = value ?? throw new DomainException(ValidationMessages.NotEmpty(nameof(Value)));
            UpdatedAtUtc = DateTime.UtcNow;
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Key)));

            var normalized = key.Trim();
            if (normalized.Length > 160)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Key)));

            return normalized;
        }
    }
}
