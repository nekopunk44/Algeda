using FluentValidation;
using System.Reflection;

namespace API.Validation
{
    public static class ServiceCollectionValidationExtensions
    {
        public static IServiceCollection AddAssemblyValidators(
            this IServiceCollection services,
            Assembly assembly)
        {
            var validators = assembly
                .GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .SelectMany(type =>
                    type.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                        .Select(i => new { ServiceType = i, ImplementationType = type }));

            foreach (var validator in validators)
            {
                services.AddScoped(validator.ServiceType, validator.ImplementationType);
            }

            return services;
        }
    }
}
