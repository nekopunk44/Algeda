using System.Globalization;
using System.Text.Json;

namespace Web.Models.Properties;

public static class PropertyValueDisplay
{
    public static string Type(string? rawType)
    {
        return rawType?.Trim() switch
        {
            "Apartment" => "Квартира",
            "House" => "Дом",
            "Commercial" => "Коммерция",
            "Land" => "Участок",
            _ => "Не указан"
        };
    }

    public static string Status(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Available" => "Доступен",
            "Reserved" => "Зарезервирован",
            "Sold" => "Продан",
            "Hidden" => "Скрыт",
            _ => "Не указан"
        };
    }

    public static string CriterionValueType(string? rawType)
    {
        return rawType?.Trim() switch
        {
            "Boolean" => "Логический",
            "Number" => "Число",
            "Text" => "Текст",
            "SingleSelect" => "Один вариант",
            "MultiSelect" => "Несколько вариантов",
            _ => "Не указан"
        };
    }

    public static string CriterionValue(string? rawValue, string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return "Не указано";
        }

        var value = rawValue.Trim();

        if (string.Equals(rawType, "Boolean", StringComparison.OrdinalIgnoreCase))
        {
            return bool.TryParse(value, out var boolValue)
                ? (boolValue ? "Да" : "Нет")
                : value;
        }

        if (string.Equals(rawType, "Number", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue)
                ? numericValue.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"))
                : value;
        }

        if (string.Equals(rawType, "MultiSelect", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var values = JsonSerializer.Deserialize<List<string>>(value);
                if (values is { Count: > 0 })
                {
                    return string.Join(", ", values);
                }
            }
            catch (JsonException)
            {
                // ignore and fallback
            }
        }

        if (string.Equals(rawType, "SingleSelect", StringComparison.OrdinalIgnoreCase)
            && value.Length >= 2
            && value.StartsWith('"')
            && value.EndsWith('"'))
        {
            return value[1..^1];
        }

        return value;
    }
}
