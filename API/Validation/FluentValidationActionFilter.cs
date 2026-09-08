using Application.DTOs.Deal;
using Application.DTOs.Property;
using Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Validation
{
    public sealed class FluentValidationActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            await PreFillSaleOwnerContact(context);

            foreach (var argument in context.ActionArguments.Values)
            {
                if (argument is null)
                {
                    continue;
                }

                var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
                if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                {
                    continue;
                }

                var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
                if (Activator.CreateInstance(validationContextType, argument) is not IValidationContext validationContext)
                {
                    continue;
                }

                var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
                foreach (var error in result.Errors)
                {
                    context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
            }

            if (!context.ModelState.IsValid)
            {
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Проверьте заполнение формы.",
                    Detail = "Исправьте ошибки в форме и повторите попытку."
                });
                return;
            }

            await next();
        }

        private static async Task PreFillSaleOwnerContact(ActionExecutingContext context)
        {
            if (!context.HttpContext.Request.Path.StartsWithSegments("/api/deals/me/sale-requests", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var email = context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Email)
                        ?? context.HttpContext.User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var clientRepository = context.HttpContext.RequestServices.GetService(typeof(IClientRepository)) as IClientRepository;
            if (clientRepository is null)
            {
                return;
            }

            var client = await clientRepository.GetByEmail(email.Trim().ToLowerInvariant());
            if (client is null)
            {
                return;
            }

            var ownerFullName = client.FullName.ToString();
            var ownerEmail = client.Email;
            var ownerPhone = client.PhoneNumber;

            var argumentPairs = context.ActionArguments.ToList();
            foreach (var pair in argumentPairs)
            {
                switch (pair.Value)
                {
                    case CreateMySaleRequest create when create.Property is not null:
                        {
                            var updatedProperty = create.Property with
                            {
                                OwnerFullName = ownerFullName,
                                OwnerEmail = ownerEmail,
                                OwnerPhoneNumber = ownerPhone
                            };

                            context.ActionArguments[pair.Key] = create with
                            {
                                Property = updatedProperty
                            };
                            break;
                        }
                    case UpdateMySaleRequest update when update.Property is not null:
                        {
                            var updatedProperty = update.Property with
                            {
                                OwnerFullName = ownerFullName,
                                OwnerEmail = ownerEmail,
                                OwnerPhoneNumber = ownerPhone
                            };

                            context.ActionArguments[pair.Key] = update with
                            {
                                Property = updatedProperty
                            };
                            break;
                        }
                }
            }
        }
    }
}
