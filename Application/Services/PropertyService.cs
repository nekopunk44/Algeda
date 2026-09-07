using Application.DTOs.Property;
using Application.DTOs.PropertyCriterionValue;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.ValueObjects;
using System.Text.Json;

namespace Application.Services
{
    public class PropertyService
    {
        private readonly IPropertyRepository _repository;
        private readonly IPropertyCriterionDefinitionRepository _criterionDefinitionRepository;
        private readonly ICurrencyRateRepository _currencyRateRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;
        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly IClientRepository _clientRepository;
        private readonly PropertyMatchingNotificationService _notificationService;
        private readonly IMapper _mapper;

        public PropertyService(
            IPropertyRepository repository,
            IPropertyCriterionDefinitionRepository criterionDefinitionRepository,
            ICurrencyRateRepository currencyRateRepository,
            IRealtorRepository realtorRepository,
            IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository,
            IIdentityAccountManager identityAccountManager,
            IClientRepository clientRepository,
            PropertyMatchingNotificationService notificationService,
            IMapper mapper)
        {
            _repository = repository;
            _criterionDefinitionRepository = criterionDefinitionRepository;
            _currencyRateRepository = currencyRateRepository;
            _realtorRepository = realtorRepository;
            _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
            _identityAccountManager = identityAccountManager;
            _clientRepository = clientRepository;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public Task<PropertyResponse> Create(CreatePropertyRequest request)
        {
            return Create(request, null, assignCurrentRealtorAsResponsible: false);
        }

        public async Task<PropertyResponse> Create(
            CreatePropertyRequest request,
            string? realtorEmail,
            bool assignCurrentRealtorAsResponsible)
        {
            var currencyCode = NormalizeCurrencyCode(request.PriceCurrency);
            var currency = await GetActiveCurrencyOrThrow(currencyCode);
            var originalPrice = new Money(request.Price, currency.Code);
            var basePrice = ConvertToBasePrice(originalPrice.Amount, currency.RateToBase);
            var responsibleRealtorId = assignCurrentRealtorAsResponsible
                ? await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty)
                : request.ResponsibleRealtorId;

            var ownerFullName = request.OwnerFullName;
            var ownerEmail = request.OwnerEmail;
            var ownerPhoneNumber = request.OwnerPhoneNumber;

            if (request.OwnerClientId.HasValue)
            {
                var client = await _clientRepository.GetById(request.OwnerClientId.Value);
                if (client != null)
                {
                    ownerFullName = $"{client.FullName.FirstName} {client.FullName.LastName}".Trim();
                    ownerEmail = client.Email;
                    ownerPhoneNumber = client.PhoneNumber;
                }
            }

            var entity = new Property(
                request.Title,
                request.Address,
                basePrice,
                request.Area,
                request.RoomsCount,
                new Location(request.Latitude, request.Longitude),
                request.Type,
                ownerFullName,
                ownerEmail,
                ownerPhoneNumber,
                originalPrice,
                responsibleRealtorId,
                request.OwnerClientId);

            entity.ReplacePhotos(request.PhotoPaths);

            await _repository.Add(entity);
            await _notificationService.NotifySubscribedClientsForProperty(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyResponse> GetById(Guid id)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyManagementResponse> GetByIdForManagement(Guid id)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            return _mapper.Map<PropertyManagementResponse>(entity);
        }

        public async Task<List<PropertyResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);

            return _mapper.Map<List<PropertyResponse>>(list);
        }

        public async Task<List<PropertyResponse>> GetForManagement(
            int limit,
            bool isAdmin,
            string? realtorEmail)
        {
            var properties = await Get(limit);
            if (isAdmin)
                return properties;

            var realtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
            return properties
                .Where(property => property.ResponsibleRealtorId == realtorId)
                .ToList();
        }

        public async Task EnsureCanManage(
            Guid propertyId,
            bool isAdmin,
            string? realtorEmail)
        {
            if (isAdmin)
                return;

            var property = await _repository.GetById(propertyId);
            var realtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
            if (property is null || property.ResponsibleRealtorId != realtorId)
                throw new NotFoundException("Объект не найден.");
        }

        public async Task<List<PropertyResponse>> GetAvailable(int limit, int offset)
        {
            var list = await _repository.GetAvailable(limit, offset);

            return _mapper.Map<List<PropertyResponse>>(list);
        }

        public async Task<PropertyResponse> UpdatePrice(UpdatePropertyPriceRequest request)
        {
            var entity = await _repository.GetById(request.PropertyId);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            var currentCurrency = await _currencyRateRepository.GetByCode(entity.OriginalPriceCurrency);
            if (currentCurrency is null)
            {
                throw new ValidationException(
                    $"Валюта {entity.OriginalPriceCurrency} не найдена. Обновление цены невозможно.");
            }

            var originalAmount = ConvertFromBasePrice(request.NewPrice, currentCurrency.RateToBase);
            entity.SetPricing(request.NewPrice, new Money(originalAmount, currentCurrency.Code));
            _repository.Update(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyResponse> Update(UpdatePropertyRequest request)
        {
            var entity = await _repository.GetById(request.PropertyId);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            var currencyCode = NormalizeCurrencyCode(request.PriceCurrency);
            var currency = await GetActiveCurrencyOrThrow(currencyCode);
            var originalPrice = new Money(request.Price, currency.Code);
            var basePrice = ConvertToBasePrice(originalPrice.Amount, currency.RateToBase);

            var ownerFullName = request.OwnerFullName;
            var ownerEmail = request.OwnerEmail;
            var ownerPhoneNumber = request.OwnerPhoneNumber;

            if (request.OwnerClientId.HasValue)
            {
                var client = await _clientRepository.GetById(request.OwnerClientId.Value);
                if (client != null)
                {
                    ownerFullName = $"{client.FullName.FirstName} {client.FullName.LastName}".Trim();
                    ownerEmail = client.Email;
                    ownerPhoneNumber = client.PhoneNumber;
                }
            }

            entity.UpdateDetails(
                request.Title,
                request.Address,
                basePrice,
                request.Area,
                request.RoomsCount,
                new Location(request.Latitude, request.Longitude),
                request.Type,
                ownerFullName,
                ownerEmail,
                ownerPhoneNumber,
                originalPrice,
                request.OwnerClientId);

            entity.ReplacePhotos(request.PhotoPaths);
            ApplyResponsibleRealtor(entity, request.ResponsibleRealtorId);

            _repository.Update(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyResponse> MarkAsSold(Guid propertyId)
        {
            var entity = await _repository.GetById(propertyId);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            entity.MarkAsSold();
            _repository.Update(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyResponse> Hide(Guid propertyId)
        {
            var entity = await _repository.GetById(propertyId);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            entity.Hide();
            _repository.Update(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task<PropertyResponse> Show(Guid propertyId)
        {
            var entity = await _repository.GetById(propertyId);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            entity.Show();
            _repository.Update(entity);

            return _mapper.Map<PropertyResponse>(entity);
        }

        public async Task Delete(Guid id)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Объект не найден.");

            _repository.Delete(entity);
        }

        public async Task<List<PropertyCriterionValueResponse>> GetCriteria(Guid propertyId)
        {
            var property = await _repository.GetById(propertyId);

            if (property is null)
                throw new NotFoundException("Объект не найден.");

            return _mapper.Map<List<PropertyCriterionValueResponse>>(property.CriterionValues);
        }

        public async Task<PropertyCriterionValueResponse> UpsertCriterionValue(
            Guid propertyId,
            UpsertPropertyCriterionValueRequest request)
        {
            var property = await _repository.GetByIdForUpdateWithCriteria(propertyId);

            if (property is null)
                throw new NotFoundException("Объект не найден.");

            var definition = await _criterionDefinitionRepository.GetByIdForUpdate(request.CriterionDefinitionId);

            if (definition is null)
                throw new NotFoundException("Критерий не найден.");

            var rawValue = BuildRawValue(request);

            var hasExistingValue = property.CriterionValues
                .Any(x => x.CriterionDefinitionId == definition.Id);

            var value = property.UpsertCriterionValue(definition, rawValue);
            if (!hasExistingValue)
            {
                // Explicitly marks a brand-new criterion value for INSERT.
                // Otherwise EF may infer UPDATE for entities with client-generated Guid keys.
                _repository.AddCriterionValue(value);
            }

            _repository.Update(property);

            return _mapper.Map<PropertyCriterionValueResponse>(value);
        }

        public async Task RemoveCriterionValue(Guid propertyId, Guid criterionDefinitionId)
        {
            var property = await _repository.GetByIdForUpdateWithCriteria(propertyId);

            if (property is null)
                throw new NotFoundException("Объект не найден.");

            var wasRemoved = property.RemoveCriterionValue(criterionDefinitionId);

            if (!wasRemoved)
                throw new NotFoundException("У объекта нет значения указанного критерия.");

            _repository.Update(property);
        }

        private async Task<CurrencyRate> GetActiveCurrencyOrThrow(string currencyCode)
        {
            var currency = await _currencyRateRepository.GetByCode(currencyCode);
            if (currency is null)
                throw new ValidationException($"Валюта {currencyCode} не найдена.");

            if (!currency.IsActive)
                throw new ValidationException($"Валюта {currencyCode} отключена.");

            return currency;
        }

        private static string NormalizeCurrencyCode(string? currencyCode)
        {
            return string.IsNullOrWhiteSpace(currencyCode)
                ? CurrencyRateService.DefaultBaseCurrencyCode
                : currencyCode.Trim().ToUpperInvariant();
        }

        private static decimal ConvertToBasePrice(decimal originalAmount, decimal rateToBase)
        {
            var value = originalAmount * rateToBase;
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ConvertFromBasePrice(decimal baseAmount, decimal rateToBase)
        {
            var value = baseAmount / rateToBase;
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string BuildRawValue(UpsertPropertyCriterionValueRequest request)
        {
            if (request.Values is not null && request.Values.Count > 0)
                return JsonSerializer.Serialize(request.Values);

            if (!string.IsNullOrWhiteSpace(request.Value))
                return request.Value.Trim();

            throw new ValidationException("Не передано значение критерия.");
        }

        private static void ApplyResponsibleRealtor(Property property, Guid? responsibleRealtorId)
        {
            if (!responsibleRealtorId.HasValue)
                return;

            property.AssignResponsibleRealtor(responsibleRealtorId.Value);
        }

        private async Task<Guid> ResolveRealtorIdByEmail(string email)
        {
            var normalizedEmail = email.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new NotFoundException("Email пользователя не найден в токене.");

            var approved = await _realtorRegistrationRequestRepository.GetLatestApprovedByEmail(normalizedEmail);
            if (approved is null)
            {
                var identityInfo = await _identityAccountManager.GetUserByEmail(normalizedEmail);
                if (identityInfo is not null)
                {
                    approved = await _realtorRegistrationRequestRepository
                        .GetLatestApprovedByIdentityUserId(identityInfo.UserId);
                }
            }

            if (approved is not null)
            {
                var realtorByPhone = await _realtorRepository.GetByPhone(approved.PhoneNumber);
                if (realtorByPhone is not null)
                    return realtorByPhone.Id;

                var fullName = new FullName(approved.FirstName, approved.LastName, approved.MiddleName);
                var realtor = await CreateRealtorProfileOrGetByPhone(fullName, approved.PhoneNumber);
                return realtor.Id;
            }

            var identityUsers = await _identityAccountManager.GetUsersWithRoles(
                normalizedEmail,
                "Realtor",
                5,
                excludeClients: false);

            var identityUser = identityUsers
                .FirstOrDefault(x => string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (identityUser is null)
                throw new NotFoundException("Профиль риелтора для текущего пользователя не найден.");

            var autoCreatedFullName = BuildRealtorFullName(identityUser.DisplayName, normalizedEmail);
            var autoCreatedPhone = BuildSyntheticPhoneNumber(identityUser.UserId);
            var autoCreatedRealtor = await CreateRealtorProfileOrGetByPhone(autoCreatedFullName, autoCreatedPhone);

            return autoCreatedRealtor.Id;
        }

        private async Task<Realtor> CreateRealtorProfileOrGetByPhone(FullName fullName, string phoneNumber)
        {
            try
            {
                var realtor = new Realtor(fullName, phoneNumber);
                await _realtorRepository.Add(realtor);
                return realtor;
            }
            catch
            {
                var existing = await _realtorRepository.GetByPhone(phoneNumber);
                if (existing is not null)
                    return existing;

                throw;
            }
        }

        private static FullName BuildRealtorFullName(string? displayName, string email)
        {
            var tokens = displayName?
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            if (tokens.Length >= 2)
            {
                var lastName = tokens[0];
                var firstName = tokens[1];
                var middleName = tokens.Length > 2
                    ? string.Join(' ', tokens.Skip(2))
                    : null;

                return new FullName(firstName, lastName, middleName);
            }

            if (tokens.Length == 1)
                return new FullName("Realtor", tokens[0], null);

            var localPart = email.Split('@', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
                return new FullName("Realtor", localPart, null);

            return new FullName("Realtor", "Account", null);
        }

        private static string BuildSyntheticPhoneNumber(Guid seed)
        {
            var raw = BitConverter.ToUInt32(seed.ToByteArray(), 0);
            var sixDigits = (raw % 1_000_000U).ToString("D6");
            return $"+37377{sixDigits}";
        }
    }
}
