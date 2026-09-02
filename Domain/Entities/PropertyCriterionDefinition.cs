using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class PropertyCriterionDefinition : BaseEntity
    {
        private const int MaxCodeLength = 100;
        private const int MaxDisplayNameLength = 200;
        private const int MaxCategoryLength = 150;
        private const int MaxDescriptionLength = 2000;

        public string Code { get; private set; }
        public string DisplayName { get; private set; }
        public string? Category { get; private set; }
        public string? Description { get; private set; }
        public PropertyCriterionValueType ValueType { get; private set; }
        public bool IsHidden { get; private set; }

        public List<PropertyCriterionOption> Options { get; private set; } = [];

        public PropertyCriterionDefinition(
            string code,
            string displayName,
            PropertyCriterionValueType valueType,
            string? category = null,
            string? description = null,
            bool isHidden = false,
            IEnumerable<(string Value, string Label, int SortOrder)>? options = null)
        {
            Code = NormalizeCode(code);
            DisplayName = NormalizeDisplayName(displayName);
            Category = NormalizeOptional(category, MaxCategoryLength);
            Description = NormalizeOptional(description, MaxDescriptionLength);
            ValidateValueType(valueType);
            ValueType = valueType;
            IsHidden = isHidden;

            if (options is not null)
            {
                ReplaceOptions(options);
            }
            else
            {
                ValidateOptionsForType(Options, ValueType);
            }
        }

        private PropertyCriterionDefinition()
        {
            Code = string.Empty;
            DisplayName = string.Empty;
        }

        public void Update(
            string code,
            string displayName,
            PropertyCriterionValueType valueType,
            string? category = null,
            string? description = null,
            bool isHidden = false,
            IEnumerable<(string Value, string Label, int SortOrder)>? options = null)
        {
            Code = NormalizeCode(code);
            DisplayName = NormalizeDisplayName(displayName);
            Category = NormalizeOptional(category, MaxCategoryLength);
            Description = NormalizeOptional(description, MaxDescriptionLength);
            ValidateValueType(valueType);
            ValueType = valueType;
            IsHidden = isHidden;

            ReplaceOptions(options ?? []);
        }

        public void SetHidden(bool isHidden)
        {
            IsHidden = isHidden;
        }

        public void ReplaceOptions(IEnumerable<(string Value, string Label, int SortOrder)> options)
        {
            var normalizedOptions = NormalizeOptions(options);

            ValidateOptionsForType(normalizedOptions, ValueType);

            var duplicate = normalizedOptions
                .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1);

            if (duplicate is not null)
                throw new DomainException($"Опции критерия содержат дубли: \"{duplicate.Key}\".");

            if (HasSameOptions(normalizedOptions))
                return;

            Options.Clear();
            Options.AddRange(normalizedOptions.Select(x =>
                new PropertyCriterionOption(Id, x.Value, x.Label, x.SortOrder)));
        }

        public bool HasOption(string optionValue)
        {
            if (string.IsNullOrWhiteSpace(optionValue))
                return false;

            return Options.Any(x =>
                string.Equals(x.Value, optionValue.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public string ResolveOptionValue(string optionValue)
        {
            var normalizedValue = optionValue.Trim();

            var option = Options.FirstOrDefault(x =>
                string.Equals(x.Value, normalizedValue, StringComparison.OrdinalIgnoreCase));

            if (option is null)
                throw new DomainException($"Опция \"{optionValue}\" не поддерживается для критерия \"{Code}\".");

            return option.Value;
        }

        private static void ValidateOptionsForType(
            IEnumerable<(string Value, string Label, int SortOrder)> options,
            PropertyCriterionValueType valueType)
        {
            var optionsList = options.ToList();

            if (valueType is PropertyCriterionValueType.SingleSelect or PropertyCriterionValueType.MultiSelect)
            {
                if (optionsList.Count == 0)
                    throw new DomainException("Для select-критерия необходимо указать хотя бы одну опцию.");
                return;
            }

            if (optionsList.Count > 0)
                throw new DomainException("Опции можно задавать только для типов SingleSelect и MultiSelect.");
        }

        private static void ValidateOptionsForType(
            IReadOnlyCollection<PropertyCriterionOption> options,
            PropertyCriterionValueType valueType)
        {
            if (valueType is PropertyCriterionValueType.SingleSelect or PropertyCriterionValueType.MultiSelect)
            {
                if (options.Count == 0)
                    throw new DomainException("Для select-критерия необходимо указать хотя бы одну опцию.");
                return;
            }

            if (options.Count > 0)
                throw new DomainException("Опции можно задавать только для типов SingleSelect и MultiSelect.");
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Code)));

            var normalized = code.Trim().ToLowerInvariant();

            if (normalized.Length > MaxCodeLength)
                throw new DomainException($"Длина {nameof(Code)} не должна превышать {MaxCodeLength} символов.");

            var hasInvalidCharacters = normalized.Any(x =>
                !char.IsLetterOrDigit(x) && x is not '_' and not '-');

            if (hasInvalidCharacters)
                throw new DomainException($"{nameof(Code)} может содержать только латиницу, цифры, '_' и '-'.");

            return normalized;
        }

        private static void ValidateValueType(PropertyCriterionValueType valueType)
        {
            if (valueType == PropertyCriterionValueType.Undefined)
                throw new DomainException("Тип значения критерия должен быть указан.");
        }

        private static string NormalizeDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(DisplayName)));

            var normalized = displayName.Trim();

            if (normalized.Length > MaxDisplayNameLength)
                throw new DomainException($"Длина {nameof(DisplayName)} не должна превышать {MaxDisplayNameLength} символов.");

            return normalized;
        }

        private static List<(string Value, string Label, int SortOrder)> NormalizeOptions(
            IEnumerable<(string Value, string Label, int SortOrder)> options)
        {
            return options
                .Select(x => (
                    Value: NormalizeOptionValue(x.Value),
                    Label: NormalizeOptionLabel(x.Label),
                    SortOrder: x.SortOrder))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool HasSameOptions(IReadOnlyList<(string Value, string Label, int SortOrder)> normalizedOptions)
        {
            if (Options.Count != normalizedOptions.Count)
                return false;

            var currentOptions = Options
                .Select(x => (
                    Value: x.Value,
                    Label: x.Label,
                    SortOrder: x.SortOrder))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = 0; index < normalizedOptions.Count; index++)
            {
                var current = currentOptions[index];
                var updated = normalizedOptions[index];

                if (!string.Equals(current.Value, updated.Value, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.Equals(current.Label, updated.Label, StringComparison.Ordinal))
                    return false;

                if (current.SortOrder != updated.SortOrder)
                    return false;
            }

            return true;
        }

        private static string NormalizeOptionValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("Значение опции критерия не может быть пустым.");

            return value.Trim();
        }

        private static string NormalizeOptionLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new DomainException("Название опции критерия не может быть пустым.");

            return label.Trim();
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            if (normalized.Length > maxLength)
                throw new DomainException($"Длина поля не должна превышать {maxLength} символов.");

            return normalized;
        }
    }
}
