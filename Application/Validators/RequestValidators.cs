using Application.DTOs.ChatMessage;
using Application.DTOs.Client;
using Application.DTOs.ClientRequirement;
using Application.DTOs.Complaint;
using Application.DTOs.DealChat;
using Application.DTOs.Deal;
using Application.DTOs.Property;
using Application.DTOs.PropertyCriterionDefinition;
using Application.DTOs.PropertyCriterionValue;
using Application.DTOs.PropertyMatching;
using Application.DTOs.Realtor;
using Application.DTOs.RealtorActivity;
using Application.DTOs.RealtorEfficiency;
using Application.DTOs.RealtorKpi;
using Application.DTOs.Review;
using Application.DTOs.Auth;
using Application.DTOs.Currency;
using Domain.Enums;
using Domain.Primitives;
using FluentValidation;

namespace Application.Validators
{
    public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.SenderId).NotEmpty();
            RuleFor(x => x.ReceiverId).NotEmpty();
            RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        }
    }


    public sealed class SendDealChatMessageRequestValidator : AbstractValidator<SendDealChatMessageRequest>
    {
        public SendDealChatMessageRequestValidator()
        {
            RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        }
    }

    public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
    {
        public CreateClientRequestValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
            RuleFor(x => x.Email).MaximumLength(256);
            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));
        }
    }

    public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
    {
        public UpdateClientRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
        }
    }

    public sealed class RequirementCriterionRequestValidator : AbstractValidator<RequirementCriterionRequest>
    {
        public RequirementCriterionRequestValidator()
        {
            RuleFor(x => x.CriterionDefinitionId).NotEmpty();
            RuleFor(x => x.Priority)
                .IsInEnum()
                .NotEqual(RequirementCriterionPriority.Undefined);

            RuleFor(x => x)
                .Must(HasExactlyOneValueSource)
                .WithMessage("Для критерия нужно указать одно значение или список значений.");

            RuleFor(x => x.Value)
                .MaximumLength(4000)
                .When(x => !string.IsNullOrWhiteSpace(x.Value));

            RuleForEach(x => x.Values!)
                .NotEmpty()
                .MaximumLength(4000)
                .When(x => x.Values is not null);

            RuleFor(x => x.Values)
                .Must(values => values is null || values.Count <= 20)
                .WithMessage("Список значений критерия может содержать не больше 20 пунктов.");

            RuleFor(x => x.Values)
                .Must(HaveNoDuplicates)
                .WithMessage("Значения критерия не должны повторяться.")
                .When(x => x.Values is not null);
        }

        private static bool HasExactlyOneValueSource(RequirementCriterionRequest request)
        {
            var hasSingle = !string.IsNullOrWhiteSpace(request.Value);
            var hasMultiple = request.Values is { Count: > 0 };
            return hasSingle ^ hasMultiple;
        }

        private static bool HaveNoDuplicates(IReadOnlyCollection<string>? values)
        {
            if (values is null)
                return true;

            var normalized = values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            return normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() == normalized.Count;
        }
    }

    public sealed class CreateRequirementRequestValidator : AbstractValidator<CreateRequirementRequest>
    {
        public CreateRequirementRequestValidator()
        {
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.DesiredType).IsInEnum();
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.SearchRadiusMeters).GreaterThanOrEqualTo(1d);
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.MinArea).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.AddressQuery)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AddressQuery));
            RuleFor(x => x.MinMatchPercentage).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.PriceWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.AreaWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.DesiredTypes)
                .Must(RequirementRequestValidation.HaveValidDesiredTypes)
                .WithMessage("Типы недвижимости не должны повторяться и не могут быть неопределенными.")
                .When(x => x.DesiredTypes is not null);

            RuleFor(x => x)
                .Must(x => x.MinPrice <= x.MaxPrice)
                .WithMessage("Минимальная цена не может быть больше максимальной.");

            RuleFor(x => x)
                .Must(x => RequirementRequestValidation.IsAreaRangeValid(x.MinArea, x.MaxArea))
                .WithMessage("Максимальная площадь не может быть меньше минимальной.");

            RuleFor(x => x)
                .Must(x => Math.Abs((x.PriceWeight + x.AreaWeight) - 1.0d) <= 0.000001d)
                .WithMessage("Сумма весов цены и площади должна быть равна 1.");

            RuleForEach(x => x.Criteria!)
                .SetValidator(new RequirementCriterionRequestValidator())
                .When(x => x.Criteria is not null);

            RuleFor(x => x.Criteria)
                .Must(HaveNoCriterionDuplicates)
                .WithMessage("Критерии подбора не должны повторяться.")
                .When(x => x.Criteria is not null);
        }

        private static bool HaveNoCriterionDuplicates(IReadOnlyCollection<RequirementCriterionRequest>? criteria)
        {
            if (criteria is null)
                return true;

            return criteria
                .GroupBy(x => x.CriterionDefinitionId)
                .All(group => group.Count() == 1);
        }
    }

    public sealed class UpdateRequirementRequestValidator : AbstractValidator<UpdateRequirementRequest>
    {
        public UpdateRequirementRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.DesiredType).IsInEnum();
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.SearchRadiusMeters).GreaterThanOrEqualTo(1d);
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.MinArea).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.AddressQuery)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AddressQuery));
            RuleFor(x => x.MinMatchPercentage).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.PriceWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.AreaWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.DesiredTypes)
                .Must(RequirementRequestValidation.HaveValidDesiredTypes)
                .WithMessage("Типы недвижимости не должны повторяться и не могут быть неопределенными.")
                .When(x => x.DesiredTypes is not null);

            RuleFor(x => x)
                .Must(x => x.MinPrice <= x.MaxPrice)
                .WithMessage("Минимальная цена не может быть больше максимальной.");

            RuleFor(x => x)
                .Must(x => RequirementRequestValidation.IsAreaRangeValid(x.MinArea, x.MaxArea))
                .WithMessage("Максимальная площадь не может быть меньше минимальной.");

            RuleFor(x => x)
                .Must(x => Math.Abs((x.PriceWeight + x.AreaWeight) - 1.0d) <= 0.000001d)
                .WithMessage("Сумма весов цены и площади должна быть равна 1.");

            RuleForEach(x => x.Criteria!)
                .SetValidator(new RequirementCriterionRequestValidator())
                .When(x => x.Criteria is not null);

            RuleFor(x => x.Criteria)
                .Must(HaveNoCriterionDuplicates)
                .WithMessage("Критерии подбора не должны повторяться.")
                .When(x => x.Criteria is not null);
        }

        private static bool HaveNoCriterionDuplicates(IReadOnlyCollection<RequirementCriterionRequest>? criteria)
        {
            if (criteria is null)
                return true;

            return criteria
                .GroupBy(x => x.CriterionDefinitionId)
                .All(group => group.Count() == 1);
        }
    }

    public sealed class CreateMyRequirementRequestValidator : AbstractValidator<CreateMyRequirementRequest>
    {
        public CreateMyRequirementRequestValidator()
        {
            RuleFor(x => x.DesiredType).IsInEnum();
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.SearchRadiusMeters).GreaterThanOrEqualTo(1d);
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.MinArea).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.AddressQuery)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AddressQuery));
            RuleFor(x => x.MinMatchPercentage).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.PriceWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.AreaWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.DesiredTypes)
                .Must(RequirementRequestValidation.HaveValidDesiredTypes)
                .WithMessage("Типы недвижимости не должны повторяться и не могут быть неопределенными.")
                .When(x => x.DesiredTypes is not null);

            RuleFor(x => x)
                .Must(x => x.MinPrice <= x.MaxPrice)
                .WithMessage("Минимальная цена не может быть больше максимальной.");

            RuleFor(x => x)
                .Must(x => RequirementRequestValidation.IsAreaRangeValid(x.MinArea, x.MaxArea))
                .WithMessage("Максимальная площадь не может быть меньше минимальной.");

            RuleFor(x => x)
                .Must(x => Math.Abs((x.PriceWeight + x.AreaWeight) - 1.0d) <= 0.000001d)
                .WithMessage("Сумма весов цены и площади должна быть равна 1.");

            RuleForEach(x => x.Criteria!)
                .SetValidator(new RequirementCriterionRequestValidator())
                .When(x => x.Criteria is not null);

            RuleFor(x => x.Criteria)
                .Must(HaveNoCriterionDuplicates)
                .WithMessage("Критерии подбора не должны повторяться.")
                .When(x => x.Criteria is not null);
        }

        private static bool HaveNoCriterionDuplicates(IReadOnlyCollection<RequirementCriterionRequest>? criteria)
        {
            if (criteria is null)
                return true;

            return criteria
                .GroupBy(x => x.CriterionDefinitionId)
                .All(group => group.Count() == 1);
        }
    }

    public sealed class UpdateMyRequirementRequestValidator : AbstractValidator<UpdateMyRequirementRequest>
    {
        public UpdateMyRequirementRequestValidator()
        {
            RuleFor(x => x.DesiredType).IsInEnum();
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.SearchRadiusMeters).GreaterThanOrEqualTo(1d);
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.MinArea).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.AddressQuery)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.AddressQuery));
            RuleFor(x => x.MinMatchPercentage).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.PriceWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.AreaWeight).InclusiveBetween(0.01d, 1d);
            RuleFor(x => x.DesiredTypes)
                .Must(RequirementRequestValidation.HaveValidDesiredTypes)
                .WithMessage("Типы недвижимости не должны повторяться и не могут быть неопределенными.")
                .When(x => x.DesiredTypes is not null);

            RuleFor(x => x)
                .Must(x => x.MinPrice <= x.MaxPrice)
                .WithMessage("Минимальная цена не может быть больше максимальной.");

            RuleFor(x => x)
                .Must(x => RequirementRequestValidation.IsAreaRangeValid(x.MinArea, x.MaxArea))
                .WithMessage("Максимальная площадь не может быть меньше минимальной.");

            RuleFor(x => x)
                .Must(x => Math.Abs((x.PriceWeight + x.AreaWeight) - 1.0d) <= 0.000001d)
                .WithMessage("Сумма весов цены и площади должна быть равна 1.");

            RuleForEach(x => x.Criteria!)
                .SetValidator(new RequirementCriterionRequestValidator())
                .When(x => x.Criteria is not null);

            RuleFor(x => x.Criteria)
                .Must(HaveNoCriterionDuplicates)
                .WithMessage("Критерии подбора не должны повторяться.")
                .When(x => x.Criteria is not null);
        }

        private static bool HaveNoCriterionDuplicates(IReadOnlyCollection<RequirementCriterionRequest>? criteria)
        {
            if (criteria is null)
                return true;

            return criteria
                .GroupBy(x => x.CriterionDefinitionId)
                .All(group => group.Count() == 1);
        }
    }

    public sealed class CreateComplaintRequestValidator : AbstractValidator<CreateComplaintRequest>
    {
        public CreateComplaintRequestValidator()
        {
            RuleFor(x => x.Category)
                .IsInEnum();
            RuleFor(x => x.ClientId).NotEmpty().When(x => x.ClientId != Guid.Empty);
            RuleFor(x => x.DealId)
                .NotEqual(Guid.Empty)
                .When(x => x.DealId.HasValue);
            RuleFor(x => x.PropertyId)
                .NotEqual(Guid.Empty)
                .When(x => x.PropertyId.HasValue);
            RuleFor(x => x.TargetRealtorId)
                .NotEqual(Guid.Empty)
                .When(x => x.TargetRealtorId.HasValue);
            RuleFor(x => x.TargetRealtorId)
                .NotNull()
                .When(x => x.Category == ComplaintCategory.Realtor)
                .WithMessage("Для жалобы на риелтора нужно указать риелтора.");
            RuleFor(x => x)
                .Must(x => x.Category != ComplaintCategory.Undefined || x.TargetRealtorId.HasValue)
                .WithMessage("Укажите категорию жалобы.");
            RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        }
    }

    public sealed class ResolveComplaintRequestValidator : AbstractValidator<ResolveComplaintRequest>
    {
        public ResolveComplaintRequestValidator()
        {
            RuleFor(x => x.ComplaintId).NotEmpty();
            RuleFor(x => x.Verdict)
                .IsInEnum()
                .NotEqual(ComplaintReviewVerdict.Undefined);
            RuleFor(x => x.Resolution).NotEmpty().MaximumLength(4000);
        }
    }

    public sealed class CompleteDealRequestValidator : AbstractValidator<CompleteDealRequest>
    {
        public CompleteDealRequestValidator()
        {
            RuleFor(x => x.DealId).NotEmpty();
            RuleFor(x => x.CommissionAmount).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.CommissionCurrency).NotEmpty().Length(3);
        }
    }

    public sealed class UpdateRealtorLevelModeRequestValidator : AbstractValidator<UpdateRealtorLevelModeRequest>
    {
        public UpdateRealtorLevelModeRequestValidator()
        {
            When(x => x.IsLevelManuallyAssigned, () =>
            {
                RuleFor(x => x.Level)
                    .NotNull()
                    .Must(x => x is RealtorLevel.Junior or RealtorLevel.Standard or RealtorLevel.Top)
                    .WithMessage("Выберите корректный уровень риелтора.");
            });
        }
    }

    public sealed class UpdateRealtorCommissionSettingsRequestValidator : AbstractValidator<UpdateRealtorCommissionSettingsRequest>
    {
        public UpdateRealtorCommissionSettingsRequestValidator()
        {
            RuleFor(x => x.Items).NotNull().Must(x => x.Count > 0);
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.Level)
                    .Must(x => x is RealtorLevel.Junior or RealtorLevel.Standard or RealtorLevel.Top)
                    .WithMessage("Настраивать можно только первые три уровня риелторов.");
                item.RuleFor(x => x.Percent).InclusiveBetween(0m, 100m);
            });
        }
    }

    public sealed class UpdateRealtorLevelSettingsRequestValidator : AbstractValidator<UpdateRealtorLevelSettingsRequest>
    {
        public UpdateRealtorLevelSettingsRequestValidator()
        {
            RuleFor(x => x.DemotionBuffer).InclusiveBetween(0d, 5d);
            RuleFor(x => x.Rules)
                .NotNull()
                .Must(x => x.Count > 0)
                .WithMessage("Нужно указать правила уровней риелторов.");

            RuleForEach(x => x.Rules).ChildRules(item =>
            {
                item.RuleFor(x => x.Level)
                    .Must(x => x is RealtorLevel.Junior or RealtorLevel.Standard or RealtorLevel.Top)
                    .WithMessage("Настраивать можно только младший, стандартный и топ-уровень риелторов.");
                item.RuleFor(x => x.MinCompletedDeals).GreaterThanOrEqualTo(0);
                item.RuleFor(x => x.MinClientTrustScore).InclusiveBetween(0d, 5d);
                item.RuleFor(x => x.MinAdminPerformanceScore).InclusiveBetween(0d, 5d);
                item.RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
            });
        }
    }

    public sealed class CreateDealRequestValidator : AbstractValidator<CreateDealRequest>
    {
        public CreateDealRequestValidator()
        {
            RuleFor(x => x.PropertyId).NotEmpty();
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.RealtorId).NotEmpty();
        }
    }

    public sealed class CreateMyDealRequestValidator : AbstractValidator<CreateMyDealRequest>
    {
        public CreateMyDealRequestValidator()
        {
            RuleFor(x => x.Source)
                .IsInEnum()
                .NotEqual(DealSource.Undefined);

            RuleFor(x => x)
                .Must(x => x.PropertyId.HasValue || x.ClientRequirementId.HasValue)
                .WithMessage("Нужно указать либо объект недвижимости, либо требование клиента.");

            RuleFor(x => x.PropertyId)
                .NotEqual(Guid.Empty)
                .When(x => x.PropertyId.HasValue);

            RuleFor(x => x.ClientRequirementId)
                .NotEqual(Guid.Empty)
                .When(x => x.ClientRequirementId.HasValue);

            RuleFor(x => x)
                .Must(x => x.Source != DealSource.Matching || x.ClientRequirementId.HasValue)
                .WithMessage("Для заявок из подбора нужно указать требование клиента.");

            RuleFor(x => x.Message)
                .MaximumLength(4000)
                .When(x => !string.IsNullOrWhiteSpace(x.Message));
        }
    }

    public sealed class AssignDealRealtorRequestValidator : AbstractValidator<AssignDealRealtorRequest>
    {
        public AssignDealRealtorRequestValidator()
        {
            RuleFor(x => x.RealtorId).NotEmpty();
        }
    }

    public sealed class UpsertDealNoteRequestValidator : AbstractValidator<UpsertDealNoteRequest>
    {
        public UpsertDealNoteRequestValidator()
        {
            RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
        }
    }

    public sealed class CreatePropertyRequestValidator : AbstractValidator<CreatePropertyRequest>
    {
        public CreatePropertyRequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.PriceCurrency)
                .Length(3)
                .When(x => !string.IsNullOrWhiteSpace(x.PriceCurrency));
            RuleFor(x => x.Area).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.RoomsCount).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.Type).IsInEnum().NotEqual(PropertyType.Undefined);
            RuleFor(x => x.OwnerFullName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.OwnerEmail).NotEmpty().MaximumLength(256).EmailAddress();
            RuleFor(x => x.OwnerPhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
            RuleFor(x => x.PhotoPaths)
                .Must(paths => paths is null || paths.Count <= 20)
                .WithMessage("Можно загрузить не более 20 фотографий.");
            RuleForEach(x => x.PhotoPaths!)
                .NotEmpty()
                .MaximumLength(500)
                .When(x => x.PhotoPaths is not null);
            RuleFor(x => x.ResponsibleRealtorId)
                .NotEqual(Guid.Empty)
                .When(x => x.ResponsibleRealtorId.HasValue);
        }
    }

    public sealed class UpdatePropertyPriceRequestValidator : AbstractValidator<UpdatePropertyPriceRequest>
    {
        public UpdatePropertyPriceRequestValidator()
        {
            RuleFor(x => x.PropertyId).NotEmpty();
            RuleFor(x => x.NewPrice).GreaterThanOrEqualTo(0.01m);
        }
    }

    public sealed class UpdatePropertyRequestValidator : AbstractValidator<UpdatePropertyRequest>
    {
        public UpdatePropertyRequestValidator()
        {
            RuleFor(x => x.PropertyId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Price).GreaterThanOrEqualTo(0.01m);
            RuleFor(x => x.PriceCurrency)
                .Length(3)
                .When(x => !string.IsNullOrWhiteSpace(x.PriceCurrency));
            RuleFor(x => x.Area).GreaterThanOrEqualTo(0.01d);
            RuleFor(x => x.RoomsCount).GreaterThanOrEqualTo(1);
            RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d);
            RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d);
            RuleFor(x => x.Type).IsInEnum().NotEqual(PropertyType.Undefined);
            RuleFor(x => x.OwnerFullName).NotEmpty().MaximumLength(300);
            RuleFor(x => x.OwnerEmail).NotEmpty().MaximumLength(256).EmailAddress();
            RuleFor(x => x.OwnerPhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
            RuleFor(x => x.PhotoPaths)
                .Must(paths => paths is null || paths.Count <= 20)
                .WithMessage("Можно загрузить не более 20 фотографий.");
            RuleForEach(x => x.PhotoPaths!)
                .NotEmpty()
                .MaximumLength(500)
                .When(x => x.PhotoPaths is not null);
            RuleFor(x => x.ResponsibleRealtorId)
                .NotEqual(Guid.Empty)
                .When(x => x.ResponsibleRealtorId.HasValue);
        }
    }


    public sealed class CreateCurrencyRateRequestValidator : AbstractValidator<CreateCurrencyRateRequest>
    {
        public CreateCurrencyRateRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10);
            RuleFor(x => x.RateToBase).GreaterThan(0m);
        }
    }

    public sealed class UpdateCurrencyRateRequestValidator : AbstractValidator<UpdateCurrencyRateRequest>
    {
        public UpdateCurrencyRateRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10);
            RuleFor(x => x.RateToBase).GreaterThan(0m);
        }
    }

    public sealed class SetCurrencyRateActiveRequestValidator : AbstractValidator<SetCurrencyRateActiveRequest>
    {
    }
    public sealed class PropertyCriterionOptionRequestValidator : AbstractValidator<PropertyCriterionOptionRequest>
    {
        public PropertyCriterionOptionRequestValidator()
        {
            RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class CreatePropertyCriterionDefinitionRequestValidator : AbstractValidator<CreatePropertyCriterionDefinitionRequest>
    {
        public CreatePropertyCriterionDefinitionRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_-]+$");
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ValueType).IsInEnum().NotEqual(PropertyCriterionValueType.Undefined);
            RuleFor(x => x.Category).MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleForEach(x => x.Options).SetValidator(new PropertyCriterionOptionRequestValidator());
        }
    }

    public sealed class UpdatePropertyCriterionDefinitionRequestValidator : AbstractValidator<UpdatePropertyCriterionDefinitionRequest>
    {
        public UpdatePropertyCriterionDefinitionRequestValidator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9_-]+$");
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ValueType).IsInEnum().NotEqual(PropertyCriterionValueType.Undefined);
            RuleFor(x => x.Category).MaximumLength(150);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleForEach(x => x.Options).SetValidator(new PropertyCriterionOptionRequestValidator());
        }
    }

    public sealed class SetPropertyCriterionDefinitionHiddenRequestValidator : AbstractValidator<SetPropertyCriterionDefinitionHiddenRequest>
    {
    }

    public sealed class UpsertPropertyCriterionValueRequestValidator : AbstractValidator<UpsertPropertyCriterionValueRequest>
    {
        public UpsertPropertyCriterionValueRequestValidator()
        {
            RuleFor(x => x.CriterionDefinitionId).NotEmpty();
            RuleFor(x => x.Value).MaximumLength(4000);

            RuleFor(x => x)
                .Must(x =>
                {
                    var hasSingleValue = !string.IsNullOrWhiteSpace(x.Value);
                    var hasMultiValues = x.Values is not null
                        && x.Values.Any(v => !string.IsNullOrWhiteSpace(v));
                    return hasSingleValue || hasMultiValues;
                })
                .WithMessage("Нужно передать одно значение или список значений.");
        }
    }

    public sealed class PropertyMatchingRequestValidator : AbstractValidator<PropertyMatchingRequest>
    {
        public PropertyMatchingRequestValidator()
        {
            RuleFor(x => x.RequirementId).NotEmpty();
            RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        }
    }

    public sealed class PropertyMatchingPreviewRequestValidator : AbstractValidator<PropertyMatchingPreviewRequest>
    {
        public PropertyMatchingPreviewRequestValidator()
        {
            RuleFor(x => x.DesiredType).IsInEnum();
            RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
            RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
            RuleFor(x => x.SearchRadiusMeters).GreaterThan(0);
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0);
            RuleFor(x => x.MaxPrice).GreaterThan(0);
            RuleFor(x => x.MinArea).GreaterThan(0);
            RuleFor(x => x.MinMatchPercentage).GreaterThan(0).LessThanOrEqualTo(1);
            RuleFor(x => x.Limit).InclusiveBetween(1, 100);

            RuleFor(x => x)
                .Must(x => x.MaxPrice >= x.MinPrice)
                .WithMessage("Проверьте диапазон цены.");

            RuleFor(x => x)
                .Must(x => !x.MaxArea.HasValue || x.MaxArea.Value >= x.MinArea)
                .WithMessage("Максимальная площадь не может быть меньше минимальной.");

            RuleForEach(x => x.Criteria).ChildRules(criteria =>
            {
                criteria.RuleFor(c => c.CriterionDefinitionId).NotEmpty();
                criteria.RuleFor(c => c.Priority).IsInEnum().NotEqual(RequirementCriterionPriority.Undefined);
                criteria.RuleFor(c => c)
                    .Must(c =>
                    {
                        var hasValue = !string.IsNullOrWhiteSpace(c.Value);
                        var hasValues = c.Values is not null && c.Values.Any(v => !string.IsNullOrWhiteSpace(v));
                        return hasValue ^ hasValues;
                    })
                    .WithMessage("Для критерия нужно указать одно значение или список значений.");
            });
        }
    }

    public sealed class CreateRealtorRequestValidator : AbstractValidator<CreateRealtorRequest>
    {
        public CreateRealtorRequestValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
        }
    }

    public sealed class RegisterClientRequestValidator : AbstractValidator<RegisterClientRequest>
    {
        public RegisterClientRequestValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
            RuleFor(x => x.ConfirmPassword).NotEmpty().Equal(x => x.Password)
                .WithMessage("Пароль и подтверждение пароля не совпадают.");
        }
    }

    public sealed class RegisterRealtorRequestValidator : AbstractValidator<RegisterRealtorRequest>
    {
        public RegisterRealtorRequestValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
            RuleFor(x => x.ConfirmPassword).NotEmpty().Equal(x => x.Password)
                .WithMessage("Пароль и подтверждение пароля не совпадают.");
        }
    }

    public sealed class UpdateRealtorRequestValidator : AbstractValidator<UpdateRealtorRequest>
    {
        public UpdateRealtorRequestValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.MiddleName).MaximumLength(100);
            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MaximumLength(32)
                .Must(PhoneNumberValidation.IsValid)
                .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
        }
    }

    public sealed class CreateActivityLogRequestValidator : AbstractValidator<CreateActivityLogRequest>
    {
        public CreateActivityLogRequestValidator()
        {
            RuleFor(x => x.RealtorId).NotEmpty();
            RuleFor(x => x.Type).IsInEnum().NotEqual(ActivityType.Undefined);
            RuleFor(x => x.Points).GreaterThanOrEqualTo(1);
        }
    }

    public sealed class UpdateActivityLogRequestValidator : AbstractValidator<UpdateActivityLogRequest>
    {
        public UpdateActivityLogRequestValidator()
        {
            RuleFor(x => x.Type).IsInEnum().NotEqual(ActivityType.Undefined);
            RuleFor(x => x.Points).GreaterThanOrEqualTo(1);
        }
    }

    public sealed class RealtorKpiRequestValidator : AbstractValidator<RealtorKpiRequest>
    {
        public RealtorKpiRequestValidator()
        {
            RuleFor(x => x.Days).GreaterThan(0);
        }
    }

    public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
    {
        public CreateReviewRequestValidator()
        {
            RuleFor(x => x.DealId).NotEmpty();
            RuleFor(x => x.RealtorId).NotEmpty();
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.Score).InclusiveBetween(1, 5);
            RuleFor(x => x.Comment).MaximumLength(4000);
        }
    }

    public sealed class CreateRealtorFeedbackRequestValidator : AbstractValidator<CreateRealtorFeedbackRequest>
    {
        public CreateRealtorFeedbackRequestValidator()
        {
            RuleFor(x => x.DealId).NotEmpty();
            RuleFor(x => x.ClientId).NotEmpty();
            RuleFor(x => x.ServiceRealtorId).NotEmpty();
            RuleFor(x => x.FormType)
                .IsInEnum()
                .Must(x => x is FeedbackFormType.Purchase or FeedbackFormType.Sale);
            RuleFor(x => x.PropertyId)
                .NotEqual(Guid.Empty)
                .When(x => x.PropertyId.HasValue);
            RuleFor(x => x.PropertyResponsibleRealtorId)
                .NotEqual(Guid.Empty)
                .When(x => x.PropertyResponsibleRealtorId.HasValue);
            RuleFor(x => x.ServiceScore).InclusiveBetween(1, 5);
            RuleFor(x => x.CommunicationScore)
                .InclusiveBetween(1, 5)
                .When(x => x.CommunicationScore.HasValue);
            RuleFor(x => x.ResponsivenessScore)
                .InclusiveBetween(1, 5)
                .When(x => x.ResponsivenessScore.HasValue);
            RuleFor(x => x.ExpertiseScore)
                .InclusiveBetween(1, 5)
                .When(x => x.ExpertiseScore.HasValue);
            RuleFor(x => x.TitleAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.CriteriaAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.DescriptionAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.PhotosAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.TitleAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.CriteriaAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.DescriptionAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.PhotosAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.Comment).MaximumLength(4000);
        }
    }

    public sealed class SubmitDealFeedbackRequestValidator : AbstractValidator<SubmitDealFeedbackRequest>
    {
        public SubmitDealFeedbackRequestValidator()
        {
            RuleFor(x => x.DealId).NotEmpty();
            RuleFor(x => x.ServiceScore).InclusiveBetween(1, 5);
            RuleFor(x => x.FormType).IsInEnum();
            RuleFor(x => x.CommunicationScore)
                .InclusiveBetween(1, 5)
                .When(x => x.CommunicationScore.HasValue);
            RuleFor(x => x.ResponsivenessScore)
                .InclusiveBetween(1, 5)
                .When(x => x.ResponsivenessScore.HasValue);
            RuleFor(x => x.ExpertiseScore)
                .InclusiveBetween(1, 5)
                .When(x => x.ExpertiseScore.HasValue);
            RuleFor(x => x.TitleAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.CriteriaAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.DescriptionAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.PhotosAccuracyScore)
                .InclusiveBetween(1, 5)
                .When(x => x.FormType == FeedbackFormType.Purchase);
            RuleFor(x => x.TitleAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.CriteriaAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.DescriptionAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.PhotosAccuracyScore).Null().When(x => x.FormType == FeedbackFormType.Sale);
            RuleFor(x => x.Comment).MaximumLength(4000);
        }
    }

    public sealed class GetRealtorScoreBreakdownRequestValidator : AbstractValidator<GetRealtorScoreBreakdownRequest>
    {
        public GetRealtorScoreBreakdownRequestValidator()
        {
            RuleFor(x => x.RealtorId).NotEmpty();
            RuleFor(x => x.Limit).InclusiveBetween(1, 200);
        }
    }

    public sealed class UpdateRealtorEligibilityTierRequestValidator : AbstractValidator<UpdateRealtorEligibilityTierRequest>
    {
        public UpdateRealtorEligibilityTierRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Name)
                .Must(x => new[] { "Junior", "Standard", "Top" }.Any(level => string.Equals(level, x, StringComparison.OrdinalIgnoreCase)))
                .WithMessage("Можно настраивать только младший, стандартный и топ-уровень риелторов.");
            RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0m);
            RuleFor(x => x.MaxPrice)
                .GreaterThanOrEqualTo(x => x.MinPrice)
                .When(x => x.MaxPrice.HasValue);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        }
    }

    public sealed class UpdateRealtorEligibilitySettingsRequestValidator : AbstractValidator<UpdateRealtorEligibilitySettingsRequest>
    {
        public UpdateRealtorEligibilitySettingsRequestValidator()
        {
            RuleFor(x => x.PriceTiers)
                .NotNull()
                .Must(x => x.Count > 0)
                .WithMessage("Нужно указать хотя бы один ценовой уровень.");
            RuleForEach(x => x.PriceTiers).SetValidator(new UpdateRealtorEligibilityTierRequestValidator());
        }
    }

    internal static class RequirementRequestValidation
    {
        public static bool HaveValidDesiredTypes(IReadOnlyCollection<PropertyType>? desiredTypes)
        {
            if (desiredTypes is null)
                return true;

            if (desiredTypes.Any(x => x == PropertyType.Undefined))
                return false;

            return desiredTypes.Distinct().Count() == desiredTypes.Count;
        }

        public static bool IsAreaRangeValid(double minArea, double? maxArea)
        {
            return !maxArea.HasValue || maxArea.Value >= minArea;
        }
    }

    internal static class PhoneNumberValidation
    {
        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var phone = value.Trim();
            var plusCount = phone.Count(c => c == '+');
            if (plusCount > 1 || (plusCount == 1 && !phone.StartsWith('+')))
            {
                return false;
            }

            if (phone.Any(c => !char.IsDigit(c) && c != '+' && c != ' ' && c != '-' && c != '(' && c != ')'))
            {
                return false;
            }

            var digitCount = phone.Count(char.IsDigit);
            return digitCount is >= 7 and <= 15;
        }
    }
}
