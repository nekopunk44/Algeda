using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Web.ModelBinding;

public sealed class FlexibleNumberModelBinderProvider : IModelBinderProvider
{
    private static readonly FlexibleNumberModelBinder Binder = new();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var modelType = context.Metadata.ModelType;
        var targetType = Nullable.GetUnderlyingType(modelType) ?? modelType;

        return targetType == typeof(decimal)
               || targetType == typeof(double)
               || targetType == typeof(float)
            ? Binder
            : null;
    }
}
