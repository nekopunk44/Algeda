using Application.DTOs.PropertyCriterionDefinition;
using Application.DTOs.PropertyCriterionValue;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using System.Globalization;
using System.Text.Json;

namespace Application.Mappers
{
    public class PropertyCriterionProfile : Profile
    {
        public PropertyCriterionProfile()
        {
            CreateMap<PropertyCriterionOption, PropertyCriterionOptionResponse>();

            CreateMap<PropertyCriterionDefinition, PropertyCriterionDefinitionResponse>()
                .ForCtorParam(nameof(PropertyCriterionDefinitionResponse.Options),
                    o => o.MapFrom(s => s.Options
                        .OrderBy(x => x.SortOrder)
                        .ThenBy(x => x.Value)));

            CreateMap<PropertyCriterionValue, PropertyCriterionValueResponse>()
                .ForCtorParam(nameof(PropertyCriterionValueResponse.CriterionCode),
                    o => o.MapFrom(s => s.CriterionDefinition != null ? s.CriterionDefinition.Code : string.Empty))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.CriterionDisplayName),
                    o => o.MapFrom(s => s.CriterionDefinition != null ? s.CriterionDefinition.DisplayName : string.Empty))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.CriterionValueType),
                    o => o.MapFrom(s => s.CriterionDefinition != null ? s.CriterionDefinition.ValueType : default))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.Category),
                    o => o.MapFrom(s => s.CriterionDefinition != null ? s.CriterionDefinition.Category : null))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.CriterionDescription),
                    o => o.MapFrom(s => s.CriterionDefinition != null ? s.CriterionDefinition.Description : null))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.IsCriterionHidden),
                    o => o.MapFrom(s => s.CriterionDefinition != null && s.CriterionDefinition.IsHidden))
                .ForCtorParam(nameof(PropertyCriterionValueResponse.DisplayValue),
                    o => o.MapFrom(s => BuildDisplayValue(s)));
        }

        private static string BuildDisplayValue(PropertyCriterionValue source)
        {
            var rawValue = source.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawValue))
                return "Не указано";

            var definition = source.CriterionDefinition;
            var valueType = definition?.ValueType ?? PropertyCriterionValueType.Undefined;

            if (valueType == PropertyCriterionValueType.Boolean)
            {
                return bool.TryParse(rawValue, out var boolValue)
                    ? (boolValue ? "Да" : "Нет")
                    : rawValue;
            }

            if (valueType == PropertyCriterionValueType.Number)
            {
                return decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue)
                    ? numericValue.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"))
                    : rawValue;
            }

            if (valueType == PropertyCriterionValueType.SingleSelect)
            {
                return ResolveOptionLabel(definition, rawValue);
            }

            if (valueType == PropertyCriterionValueType.MultiSelect)
            {
                var values = ParseValues(rawValue);
                if (values.Count == 0)
                    return "Не указано";

                var labels = values
                    .Select(value => ResolveOptionLabel(definition, value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return labels.Count > 0
                    ? string.Join(", ", labels)
                    : "Не указано";
            }

            return rawValue;
        }

        private static List<string> ParseValues(string rawValue)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(rawValue);
                if (parsed is { Count: > 0 })
                {
                    return parsed
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToList();
                }
            }
            catch (JsonException)
            {
                // ignore and use fallback
            }

            return rawValue
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(Unquote)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private static string ResolveOptionLabel(PropertyCriterionDefinition? definition, string rawValue)
        {
            var normalizedValue = Unquote(rawValue);
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return "Не указано";

            var option = definition?.Options
                .FirstOrDefault(x => string.Equals(x.Value, normalizedValue, StringComparison.OrdinalIgnoreCase));

            return option?.Label ?? normalizedValue;
        }

        private static string Unquote(string value)
        {
            var normalized = value.Trim();
            if (normalized.Length >= 2
                && normalized.StartsWith('"')
                && normalized.EndsWith('"'))
            {
                return normalized[1..^1];
            }

            return normalized;
        }
    }
}
