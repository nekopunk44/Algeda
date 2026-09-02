using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Web.ModelBinding;

public sealed class FlexibleNumberModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext is null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        var rawValue = valueResult.FirstValue;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Task.CompletedTask;
        }

        if (TryParse(rawValue, bindingContext.ModelType, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"The value '{rawValue}' is not valid for {bindingContext.ModelName}.");

        return Task.CompletedTask;
    }

    private static bool TryParse(string rawValue, Type modelType, out object? parsed)
    {
        parsed = null;

        var targetType = Nullable.GetUnderlyingType(modelType) ?? modelType;
        var normalized = rawValue.Trim();
        var normalizedWithDot = normalized.Replace(',', '.');
        var normalizedWithComma = normalized.Replace('.', ',');

        if (targetType == typeof(double))
        {
            if (TryParseDouble(normalized, out var value)
                || TryParseDouble(normalizedWithDot, out value)
                || TryParseDouble(normalizedWithComma, out value))
            {
                parsed = value;
                return true;
            }

            return false;
        }

        if (targetType == typeof(decimal))
        {
            if (TryParseDecimal(normalized, out var value)
                || TryParseDecimal(normalizedWithDot, out value)
                || TryParseDecimal(normalizedWithComma, out value))
            {
                parsed = value;
                return true;
            }

            return false;
        }

        if (targetType == typeof(float))
        {
            if (TryParseFloat(normalized, out var value)
                || TryParseFloat(normalizedWithDot, out value)
                || TryParseFloat(normalizedWithComma, out value))
            {
                parsed = value;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryParseDouble(string value, out double parsed)
    {
        return double.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.CurrentCulture,
                   out parsed)
               || double.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture,
                   out parsed)
               || double.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.GetCultureInfo("ru-RU"),
                   out parsed)
               || double.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.GetCultureInfo("en-US"),
                   out parsed);
    }

    private static bool TryParseDecimal(string value, out decimal parsed)
    {
        return decimal.TryParse(
                   value,
                   NumberStyles.Number,
                   CultureInfo.CurrentCulture,
                   out parsed)
               || decimal.TryParse(
                   value,
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out parsed)
               || decimal.TryParse(
                   value,
                   NumberStyles.Number,
                   CultureInfo.GetCultureInfo("ru-RU"),
                   out parsed)
               || decimal.TryParse(
                   value,
                   NumberStyles.Number,
                   CultureInfo.GetCultureInfo("en-US"),
                   out parsed);
    }

    private static bool TryParseFloat(string value, out float parsed)
    {
        return float.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.CurrentCulture,
                   out parsed)
               || float.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.InvariantCulture,
                   out parsed)
               || float.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.GetCultureInfo("ru-RU"),
                   out parsed)
               || float.TryParse(
                   value,
                   NumberStyles.Float | NumberStyles.AllowThousands,
                   CultureInfo.GetCultureInfo("en-US"),
                   out parsed);
    }
}
