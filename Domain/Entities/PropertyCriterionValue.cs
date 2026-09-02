using System.Globalization;
using System.Text.Json;
using Domain.Common;
using Domain.Enums;
using Domain.Primitives;

namespace Domain.Entities
{
    public class PropertyCriterionValue : BaseEntity
    {
        private const int MaxValueLength = 4000;

        public Guid PropertyId { get; private set; }
        public Guid CriterionDefinitionId { get; private set; }
        public string Value { get; private set; }

        public Property? Property { get; private set; }
        public PropertyCriterionDefinition? CriterionDefinition { get; private set; }

        public PropertyCriterionValue(
            Guid propertyId,
            PropertyCriterionDefinition definition,
            string rawValue)
        {
            PropertyId = propertyId;
            CriterionDefinitionId = definition?.Id
                ?? throw new DomainException(ValidationMessages.NotNull(nameof(definition)));
            CriterionDefinition = definition;

            Value = NormalizeValue(definition, rawValue);
            ValidateCore();
        }

        private PropertyCriterionValue()
        {
            Value = string.Empty;
        }

        public void SetValue(PropertyCriterionDefinition definition, string rawValue)
        {
            if (definition is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(definition)));

            if (definition.Id != CriterionDefinitionId)
                throw new DomainException("Критерий значения не совпадает с определением.");

            CriterionDefinition = definition;
            Value = NormalizeValue(definition, rawValue);
            ValidateCore();
        }

        private static string NormalizeValue(PropertyCriterionDefinition definition, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new DomainException("Значение критерия не может быть пустым.");

            var normalizedRawValue = rawValue.Trim();

            return definition.ValueType switch
            {
                PropertyCriterionValueType.Boolean => NormalizeBoolean(normalizedRawValue),
                PropertyCriterionValueType.Number => NormalizeNumber(normalizedRawValue),
                PropertyCriterionValueType.Text => NormalizeText(normalizedRawValue),
                PropertyCriterionValueType.SingleSelect => NormalizeSingleSelect(definition, normalizedRawValue),
                PropertyCriterionValueType.MultiSelect => NormalizeMultiSelect(definition, normalizedRawValue),
                _ => throw new DomainException("Тип значения критерия не поддерживается.")
            };
        }

        private static string NormalizeBoolean(string value)
        {
            if (!bool.TryParse(value, out var parsed))
                throw new DomainException("Для данного критерия ожидается boolean-значение (true/false).");

            return parsed ? "true" : "false";
        }

        private static string NormalizeNumber(string value)
        {
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                throw new DomainException("Для данного критерия ожидается числовое значение.");

            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        private static string NormalizeText(string value)
        {
            return value;
        }

        private static string NormalizeSingleSelect(PropertyCriterionDefinition definition, string value)
        {
            return definition.ResolveOptionValue(value);
        }

        private static string NormalizeMultiSelect(PropertyCriterionDefinition definition, string value)
        {
            List<string>? values;

            if (value.StartsWith("["))
            {
                values = JsonSerializer.Deserialize<List<string>>(value);
            }
            else
            {
                values = value
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            if (values is null || values.Count == 0)
                throw new DomainException("Для MultiSelect-критерия требуется непустой список значений.");

            var normalizedValues = values
                .Select(definition.ResolveOptionValue)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedValues.Count == 0)
                throw new DomainException("Для MultiSelect-критерия требуется непустой список значений.");

            return JsonSerializer.Serialize(normalizedValues);
        }

        private void ValidateCore()
        {
            if (PropertyId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(PropertyId)));

            if (CriterionDefinitionId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(CriterionDefinitionId)));

            if (string.IsNullOrWhiteSpace(Value))
                throw new DomainException("Значение критерия не может быть пустым.");

            if (Value.Length > MaxValueLength)
                throw new DomainException($"Значение критерия не должно превышать {MaxValueLength} символов.");
        }
    }
}
