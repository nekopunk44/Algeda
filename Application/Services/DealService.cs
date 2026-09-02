using Application.DTOs.Deal;
using Application.Exceptions;
using Application.Interfaces;
using Application.DTOs.Property;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Services
{
    public class DealService
    {
        private const int BuyerRequestPriorityHours = 24;

        private readonly IDealRepository _repository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IClientRequirementRepository _clientRequirementRepository;
        private readonly IPropertyCriterionDefinitionRepository _criterionDefinitionRepository;
        private readonly ICurrencyRateRepository _currencyRateRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;
        private readonly IRealtorCommissionSettingsService _commissionSettingsService;
        private readonly IRealtorLevelCalculationService _levelCalculationService;
        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly RealtorDealEligibilityService _eligibilityService;
        private readonly ILogger<DealService> _logger;
        private readonly IMapper _mapper;

        public DealService(
            IDealRepository repository,
            IPropertyRepository propertyRepository,
            IClientRepository clientRepository,
            IClientRequirementRepository clientRequirementRepository,
            IPropertyCriterionDefinitionRepository criterionDefinitionRepository,
            ICurrencyRateRepository currencyRateRepository,
            IRealtorRepository realtorRepository,
            IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository,
            IRealtorCommissionSettingsService commissionSettingsService,
            IRealtorLevelCalculationService levelCalculationService,
            IIdentityAccountManager identityAccountManager,
            RealtorDealEligibilityService eligibilityService,
            ILogger<DealService> logger,
            IMapper mapper)
        {
            _repository = repository;
            _propertyRepository = propertyRepository;
            _clientRepository = clientRepository;
            _clientRequirementRepository = clientRequirementRepository;
            _criterionDefinitionRepository = criterionDefinitionRepository;
            _currencyRateRepository = currencyRateRepository;
            _realtorRepository = realtorRepository;
            _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
            _commissionSettingsService = commissionSettingsService;
            _levelCalculationService = levelCalculationService;
            _identityAccountManager = identityAccountManager;
            _eligibilityService = eligibilityService;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<DealResponse> Create(CreateDealRequest request)
        {
            var property = await _propertyRepository.GetById(request.PropertyId);
            if (property is null)
                throw new NotFoundException("Объект не найден.");

            if (property.Status != PropertyStatus.Available)
                throw new ValidationException("Сделку можно создать только для доступного объекта.");

            var client = await _clientRepository.GetById(request.ClientId);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var realtor = await _realtorRepository.GetById(request.RealtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var deal = _mapper.Map<Deal>(request);

            await _repository.Add(deal);

            return _mapper.Map<DealResponse>(deal);
        }

        public async Task<DealWorkflowResponse> CreateMyRequest(string email, CreateMyDealRequest request)
        {
            var clientId = await ResolveClientIdByEmail(email);

            Property? propertyForPriority = null;
            Guid? propertyId = request.PropertyId;
            if (propertyId.HasValue)
            {
                propertyForPriority = await _propertyRepository.GetById(propertyId.Value);
                if (propertyForPriority is null)
                    throw new NotFoundException("Объект не найден.");
            }

            Guid? requirementId = request.ClientRequirementId;
            if (requirementId.HasValue)
            {
                var requirement = await _clientRequirementRepository.GetById(requirementId.Value);
                if (requirement is null)
                    throw new NotFoundException("Требование клиента не найдено.");

                if (requirement.ClientId != clientId)
                    throw new ValidationException("Нельзя использовать чужое требование клиента.");
            }

            var deal = Deal.CreateIncoming(
                clientId,
                request.Source,
                propertyId,
                requirementId,
                request.Message);

            await ApplyBuyerRequestPriority(deal, propertyForPriority);

            await _repository.Add(deal);

            // Скрываем объект недвижимости при создании заявки на покупку
            if (propertyForPriority is not null && request.Source != DealSource.Sale)
            {
                await TryHidePropertyForDeal(propertyForPriority);
            }

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<DealResponse> GetById(Guid id)
        {
            var deal = await _repository.GetById(id);

            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            return _mapper.Map<DealResponse>(deal);
        }

        public async Task<DealWorkflowResponse> GetWorkflowByIdForCurrentUser(
            Guid dealId,
            bool isAdmin,
            bool isRealtor,
            string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (isAdmin)
                return (await MapWorkflowResponses([deal]))[0];

            if (!isRealtor)
                throw new ValidationException("Просмотр заявок доступен только риелторам и администраторам.");

            var currentRealtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
            if (!deal.IsIncoming)
            {
                if (deal.RealtorId != currentRealtorId)
                    throw new NotFoundException("Сделка не найдена.");
            }
            else if (await HasExclusiveActivePriority(deal)
                     && deal.PriorityRealtorId != currentRealtorId)
            {
                throw new NotFoundException("Сделка не найдена.");
            }

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<SaleRequestDetailsResponse> CreateMySaleRequest(string email, CreateMySaleRequest request)
        {
            var clientId = await ResolveClientIdByEmail(email);
            var ownerContact = await ResolvePropertyOwnerContact(clientId);

            var currencyCode = NormalizeCurrencyCode(request.Property.PriceCurrency);
            var currency = await GetActiveCurrencyOrThrow(currencyCode);
            var originalPrice = new Money(request.Property.Price, currency.Code);
            var basePrice = ConvertToBasePrice(originalPrice.Amount, currency.RateToBase);

            var property = new Property(
                request.Property.Title,
                request.Property.Address,
                basePrice,
                request.Property.Area,
                request.Property.RoomsCount,
                new Location(request.Property.Latitude, request.Property.Longitude),
                request.Property.Type,
                ownerContact.FullName,
                ownerContact.Email,
                ownerContact.PhoneNumber,
                originalPrice);

            property.ReplacePhotos(request.Property.PhotoPaths);
            await ApplySaleCriteria(property, request.Criteria, registerNewValues: false);

            property.Hide();
            await _propertyRepository.Add(property);

            var deal = Deal.CreateIncoming(
                clientId,
                DealSource.Sale,
                property.Id,
                null,
                request.Message);

            await _repository.Add(deal);

            return await BuildSaleRequestDetailsResponse(deal, property);
        }

        public async Task<SaleRequestDetailsResponse> GetSaleRequestByIdForCurrentUser(
            Guid dealId,
            bool isAdmin,
            bool isRealtor,
            string? realtorEmail)
        {
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null || deal.Source != DealSource.Sale)
                throw new NotFoundException("Заявка на продажу не найдена.");

            if (isAdmin)
            {
                return await BuildSaleRequestDetailsResponse(deal);
            }

            if (!isRealtor)
                throw new ValidationException("Просмотр заявки на продажу доступен только риелторам и администраторам.");

            await EnsureRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);
            return await BuildSaleRequestDetailsResponse(deal);
        }

        public async Task<List<DealWorkflowResponse>> GetMySaleRequests(
            string email,
            int limit,
            string? search,
            DealStatus? status)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var clientId = await ResolveClientIdByEmail(email);
            var deals = await _repository.GetByClient(clientId);
            var mapped = await MapWorkflowResponses(deals);
            return ApplyWorkflowFilters(mapped, search, status, safeLimit, DealSource.Sale);
        }

        public async Task<List<DealWorkflowResponse>> GetMyDealsForClientCenter(
            string email,
            int limit,
            string? search,
            DealStatus? status,
            string? scope)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var clientId = await ResolveClientIdByEmail(email);
            var deals = await _repository.GetByClient(clientId);
            var mapped = await MapWorkflowResponses(deals);

            var filtered = ApplyWorkflowFilters(
                mapped,
                search,
                status,
                Math.Max(mapped.Count, safeLimit),
                sourceFilter: null);

            return ApplyClientScopeFilter(filtered, scope)
                .Take(safeLimit)
                .ToList();
        }

        public async Task<DealWorkflowResponse> GetMyDealByIdForClientCenter(string email, Guid dealId)
        {
            var clientId = await ResolveClientIdByEmail(email);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null || deal.ClientId != clientId)
                throw new NotFoundException("Сделка не найдена.");

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<SaleRequestDetailsResponse> GetMySaleRequestById(string email, Guid dealId)
        {
            var clientId = await ResolveClientIdByEmail(email);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null
                || deal.ClientId != clientId
                || deal.Source != DealSource.Sale)
            {
                throw new NotFoundException("Заявка на продажу не найдена.");
            }

            return await BuildSaleRequestDetailsResponse(deal);
        }

        public async Task<SaleRequestDetailsResponse> UpdateMySaleRequest(
            string email,
            Guid dealId,
            UpdateMySaleRequest request)
        {
            var clientId = await ResolveClientIdByEmail(email);

            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null
                || deal.ClientId != clientId
                || deal.Source != DealSource.Sale)
            {
                throw new NotFoundException("Заявка на продажу не найдена.");
            }

            if (deal.Status != DealStatus.Created)
                throw new ValidationException("Редактировать можно только входящую заявку на продажу.");

            var ownerContact = await ResolvePropertyOwnerContact(deal.ClientId);
            return await UpdateSaleRequestCore(deal, request, ownerContact);
        }

        public async Task<SaleRequestDetailsResponse> UpdateSaleRequestByRealtorOrAdmin(
            Guid dealId,
            UpdateMySaleRequest request,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null || deal.Source != DealSource.Sale)
                throw new NotFoundException("Заявка на продажу не найдена.");

            if (!isAdmin)
            {
                await EnsureRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);
            }

            if (deal.Status is DealStatus.Completed or DealStatus.Cancelled)
                throw new ValidationException("Нельзя редактировать завершённую или отменённую заявку на продажу.");

            var ownerContact = await ResolvePropertyOwnerContact(deal.ClientId);
            return await UpdateSaleRequestCore(deal, request, ownerContact);
        }

        private async Task<SaleRequestDetailsResponse> UpdateSaleRequestCore(
            Deal deal,
            UpdateMySaleRequest request,
            PropertyOwnerContact ownerContact)
        {
            if (deal.PropertyId == Guid.Empty)
                throw new ValidationException("Для заявки не найден объект недвижимости.");

            var property = await _propertyRepository.GetByIdForUpdateWithCriteria(deal.PropertyId);
            if (property is null)
                throw new NotFoundException("Объект недвижимости для заявки не найден.");

            var currencyCode = NormalizeCurrencyCode(request.Property.PriceCurrency);
            var currency = await GetActiveCurrencyOrThrow(currencyCode);
            var originalPrice = new Money(request.Property.Price, currency.Code);
            var basePrice = ConvertToBasePrice(originalPrice.Amount, currency.RateToBase);

            property.UpdateDetails(
                request.Property.Title,
                request.Property.Address,
                basePrice,
                request.Property.Area,
                request.Property.RoomsCount,
                new Location(request.Property.Latitude, request.Property.Longitude),
                request.Property.Type,
                ownerContact.FullName,
                ownerContact.Email,
                ownerContact.PhoneNumber,
                originalPrice);

            property.ReplacePhotos(request.Property.PhotoPaths);

            if (property.Status != PropertyStatus.Hidden)
            {
                property.Hide();
            }

            var existingCriterionIds = property.CriterionValues
                .Select(x => x.CriterionDefinitionId)
                .ToList();

            foreach (var criterionId in existingCriterionIds)
            {
                property.RemoveCriterionValue(criterionId);
            }

            await ApplySaleCriteria(property, request.Criteria, registerNewValues: true);
            _propertyRepository.Update(property);

            deal.UpdateRequestMessage(request.Message);
            _repository.Update(deal);

            return await BuildSaleRequestDetailsResponse(deal, property);
        }

        public async Task<DealWorkflowResponse> CancelMySaleRequest(string email, Guid dealId)
        {
            var clientId = await ResolveClientIdByEmail(email);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null
                || deal.ClientId != clientId
                || deal.Source != DealSource.Sale)
            {
                throw new NotFoundException("Заявка на продажу не найдена.");
            }

            if (deal.Status == DealStatus.Completed)
                throw new ValidationException("Нельзя отменить завершённую заявку.");

            // Запоминаем до изменения статуса
            var propertyId = deal.PropertyId;

            deal.Cancel();
            _repository.Update(deal);

            // Раскрываем объект, если нет других активных заявок на него
            if (propertyId != Guid.Empty && deal.Source != DealSource.Sale)
            {
                await TryRestorePropertyAfterDealDeactivated(propertyId, deal.Id);
            }

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<List<DealResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);

            return _mapper.Map<List<DealResponse>>(list);
        }

        public async Task<List<DealWorkflowResponse>> GetIncomingForDashboard(
            bool isAdmin,
            string? realtorEmail,
            int limit,
            string? search,
            DealSource? source)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var scanLimit = Math.Max(safeLimit * 5, 200);

            IReadOnlyList<Deal> deals;
            if (isAdmin)
            {
                deals = await _repository.GetIncoming(scanLimit);
            }
            else
            {
                var realtorId = await TryResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                deals = await _repository.GetIncoming(scanLimit);
                if (realtorId.HasValue)
                {
                    deals = await FilterIncomingByPriority(deals, realtorId.Value);
                }
                else
                {
                    deals = await FilterIncomingByPriority(deals, null);
                }
            }

            var mapped = await MapWorkflowResponses(deals);
            return ApplyWorkflowFilters(mapped, search, DealStatus.Created, safeLimit, source);
        }

        public async Task<List<DealWorkflowResponse>> GetMyDealsForDashboard(
            string realtorEmail,
            int limit,
            string? search,
            DealStatus? status,
            DealSource? source)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var realtorId = await TryResolveRealtorIdByEmail(realtorEmail);
            if (!realtorId.HasValue)
            {
                return [];
            }

            var deals = await _repository.GetByRealtor(realtorId.Value);
            var mapped = await MapWorkflowResponses(deals);
            return ApplyWorkflowFilters(mapped, search, status, safeLimit, source);
        }

        public async Task<List<DealWorkflowResponse>> GetAllDealsForAdmin(
            int limit,
            string? search,
            DealStatus? status,
            DealSource? source)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var scanLimit = Math.Max(safeLimit * 5, 200);
            var deals = await _repository.Get(scanLimit);
            var mapped = await MapWorkflowResponses(deals);
            return ApplyWorkflowFilters(mapped, search, status, safeLimit, source);
        }

        public async Task<List<DealResponse>> GetByRealtor(Guid realtorId)
        {
            var list = await _repository.GetByRealtor(realtorId);

            return _mapper.Map<List<DealResponse>>(list);
        }

        public async Task<List<DealResponse>> GetByRealtorForCurrentUser(
            Guid realtorId,
            bool isAdmin,
            string? realtorEmail)
        {
            if (!isAdmin)
            {
                var currentRealtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                if (currentRealtorId != realtorId)
                    throw new NotFoundException("Риелтор не найден.");
            }

            return await GetByRealtor(realtorId);
        }

        public async Task<List<DealResponse>> GetByClient(Guid clientId)
        {
            var list = await _repository.GetByClient(clientId);

            return _mapper.Map<List<DealResponse>>(list);
        }

        public async Task<List<DealResponse>> GetByClientForCurrentUser(
            Guid clientId,
            bool isAdmin,
            string? clientEmail)
        {
            if (!isAdmin)
            {
                var currentClientId = await ResolveClientIdByEmail(clientEmail ?? string.Empty);
                if (currentClientId != clientId)
                    throw new NotFoundException("Клиент не найден.");
            }

            return await GetByClient(clientId);
        }

        public async Task<DealWorkflowResponse> AcceptIncoming(Guid dealId, string realtorEmail)
        {
            var realtorId = await ResolveRealtorIdByEmail(realtorEmail);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            await EnsurePriorityAllowsAccept(deal, realtorId);
            await EnsureEligibilityForExpensiveDeal(realtorId, deal);
            deal.AcceptIncoming(realtorId);
            _repository.Update(deal);
            await AssignSalePropertyResponsibleRealtor(deal, realtorId);

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<DealWorkflowResponse> RejectIncoming(Guid dealId, string realtorEmail)
        {
            var realtorId = await ResolveRealtorIdByEmail(realtorEmail);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (await TryReleasePriorityOnRejection(deal, realtorId))
            {
                _repository.Update(deal);
                return (await MapWorkflowResponses([deal]))[0];
            }

            var propertyId = deal.PropertyId;
            deal.RejectIncoming(realtorId);
            _repository.Update(deal);

            if (propertyId != Guid.Empty && deal.Source != DealSource.Sale)
            {
                await TryRestorePropertyAfterDealDeactivated(propertyId, deal.Id);
            }

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<DealWorkflowResponse> ReleaseByRealtor(Guid dealId, string realtorEmail)
        {
            var realtorId = await ResolveRealtorIdByEmail(realtorEmail);
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            // Запоминаем до изменения статуса
            var propertyId = deal.PropertyId;
            deal.ReleaseRealtor(realtorId);
            _repository.Update(deal);

            // Раскрываем объект, если нет других активных заявок на него
            if (propertyId != Guid.Empty && deal.Source != DealSource.Sale)
            {
                await TryRestorePropertyAfterDealDeactivated(propertyId, deal.Id);
            }

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<DealWorkflowResponse> AssignRealtor(Guid dealId, Guid realtorId)
        {
            var realtor = await _realtorRepository.GetById(realtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            await EnsureEligibilityForExpensiveDeal(realtorId, deal);
            deal.ReassignRealtor(realtorId);
            _repository.Update(deal);

            return (await MapWorkflowResponses([deal]))[0];
        }

        public async Task<SaleRequestDetailsResponse> PublishSaleProperty(
            Guid dealId,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetByIdWithNotes(dealId);
            if (deal is null || deal.Source != DealSource.Sale)
                throw new NotFoundException("Заявка на продажу не найдена.");

            if (!isAdmin)
                await EnsureAssignedRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);

            if (deal.PropertyId == Guid.Empty)
                throw new ValidationException("Для заявки не найден объект недвижимости.");

            var property = await _propertyRepository.GetByIdForUpdateWithCriteria(deal.PropertyId);
            if (property is null)
                throw new NotFoundException("Объект недвижимости для заявки не найден.");

            if (property.Status == PropertyStatus.Hidden)
            {
                property.Show();
                _propertyRepository.Update(property);
            }

            return await BuildSaleRequestDetailsResponse(deal, property);
        }

        public async Task<DealNoteResponse> AddNote(
            Guid dealId,
            string text,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            Guid? authorRealtorId = null;
            if (!isAdmin)
            {
                var realtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                if (!deal.IsIncoming && deal.RealtorId != realtorId)
                    throw new NotFoundException("Сделка не найдена.");

                authorRealtorId = realtorId;
            }

            var note = await _repository.AddNote(dealId, text, authorRealtorId);
            if (note is null)
                throw new NotFoundException("Сделка не найдена.");

            return _mapper.Map<DealNoteResponse>(note);
        }

        public async Task<DealNoteResponse> UpdateNote(
            Guid dealId,
            Guid noteId,
            string text,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (!isAdmin)
            {
                var realtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                if (!deal.IsIncoming && deal.RealtorId != realtorId)
                    throw new NotFoundException("Сделка не найдена.");
            }

            var note = await _repository.UpdateNote(dealId, noteId, text);
            if (note is null)
                throw new NotFoundException("Заметка сделки не найдена.");

            return _mapper.Map<DealNoteResponse>(note);
        }

        public async Task DeleteNote(
            Guid dealId,
            Guid noteId,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (!isAdmin)
            {
                var realtorId = await ResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                if (!deal.IsIncoming && deal.RealtorId != realtorId)
                    throw new NotFoundException("Сделка не найдена.");
            }

            var deleted = await _repository.DeleteNote(dealId, noteId);
            if (!deleted)
                throw new NotFoundException("Заметка сделки не найдена.");
        }

        public async Task<IReadOnlyList<DealStatusCountItemResponse>> GetStatusCountsForDashboard(
            bool isAdmin,
            string? realtorEmail,
            DealSource? source)
        {
            IReadOnlyCollection<Deal> dealsSource;
            if (isAdmin)
            {
                dealsSource = await _repository.Get(500);
            }
            else
            {
                var realtorId = await TryResolveRealtorIdByEmail(realtorEmail ?? string.Empty);
                dealsSource = realtorId.HasValue
                    ? await _repository.GetIncomingOrAssignedToRealtor(realtorId.Value, 500)
                    : await _repository.GetIncoming(500);

                if (realtorId.HasValue)
                {
                    dealsSource = await FilterIncomingByPriority(dealsSource, realtorId.Value, includeAssigned: true);
                }
                else
                {
                    dealsSource = await FilterIncomingByPriority(dealsSource, null, includeAssigned: true);
                }
            }

            if (source.HasValue && source.Value != DealSource.Undefined)
            {
                dealsSource = dealsSource
                    .Where(x => x.Source == source.Value)
                    .ToList();
            }

            return dealsSource
                .GroupBy(x => x.Status)
                .Select(x => new DealStatusCountItemResponse(x.Key, x.Count()))
                .OrderBy(x => x.Status)
                .ToList();
        }

        public async Task<List<RealtorLookupResponse>> SearchRealtorsForAssignment(string? query, int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var normalized = query?.Trim();

            var realtors = await _realtorRepository.GetActive();
            var approvedRequests = await _realtorRegistrationRequestRepository.GetApprovedByPhoneNumbers(
                realtors.Select(x => x.PhoneNumber).Distinct().ToList());

            var emailByPhone = approvedRequests
                .GroupBy(x => x.PhoneNumber)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderByDescending(r => r.CreatedDate).First().Email,
                    StringComparer.OrdinalIgnoreCase);

            var result = realtors
                .Select(realtor =>
                {
                    emailByPhone.TryGetValue(realtor.PhoneNumber, out var email);
                    return new RealtorLookupResponse(
                        realtor.Id,
                        realtor.FullName.ToString(),
                        realtor.PhoneNumber,
                        email);
                });

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                result = result.Where(x =>
                    ContainsIgnoreCase(x.FullName, normalized)
                    || ContainsIgnoreCase(x.PhoneNumber, normalized)
                    || ContainsIgnoreCase(x.Email, normalized));
            }

            return result
                .OrderBy(x => x.FullName)
                .ThenBy(x => x.PhoneNumber)
                .Take(safeLimit)
                .ToList();
        }

        public async Task<DealResponse> Complete(
            CompleteDealRequest request,
            bool isAdmin,
            string? realtorEmail)
        {
            var deal = await _repository.GetById(request.DealId);

            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (!isAdmin)
                await EnsureAssignedRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);

            var realtor = await _realtorRepository.GetById(deal.RealtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var realtorCommissionPercent = _commissionSettingsService.GetPercentForLevel(realtor.Level);
            deal.Complete(request.CommissionAmount, request.CommissionCurrency, realtorCommissionPercent);
            _repository.Update(deal);
            await _levelCalculationService.Recalculate(realtor.Id);

            if (deal.PropertyId != Guid.Empty)
            {
                var property = await _propertyRepository.GetById(deal.PropertyId);
                if (property is not null && property.Status != PropertyStatus.Sold)
                {
                    property.MarkAsSold();
                    _propertyRepository.Update(property);
                }

                await SyncSaleRequestAfterDealCompletion(deal);
            }

            return _mapper.Map<DealResponse>(deal);
        }

        public async Task Cancel(Guid dealId, bool isAdmin, string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);

            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (!isAdmin)
                await EnsureAssignedRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);

            var propertyId = deal.PropertyId;
            deal.Cancel();

            _repository.Update(deal);

            // Раскрываем объект, если нет других активных заявок на него
            if (propertyId != Guid.Empty && deal.Source != DealSource.Sale)
            {
                await TryRestorePropertyAfterDealDeactivated(propertyId, deal.Id);
            }
        }

        public async Task Delete(Guid dealId, bool isAdmin, string? realtorEmail)
        {
            var deal = await _repository.GetById(dealId);

            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (!isAdmin)
                await EnsureAssignedRealtorAccessToDeal(deal, realtorEmail ?? string.Empty);

            _repository.Delete(deal);
        }

        /// <summary>
        /// Скрывает объект недвижимости при создании заявки на покупку.
        /// Игнорирует, если объект уже скрыт или продан.
        /// </summary>
        private async Task TryHidePropertyForDeal(Property property)
        {
            if (property.Status == PropertyStatus.Available)
            {
                property.Hide();
                _propertyRepository.Update(property);
            }
        }

        /// <summary>
        /// Восстанавливает объект недвижимости (Hidden → Available) после отмены/отклонения заявки,
        /// но только если нет других активных (не Cancelled, не Completed) заявок на этот объект.
        /// </summary>
        private async Task TryRestorePropertyAfterDealDeactivated(Guid propertyId, Guid excludeDealId)
        {
            try
            {
                var property = await _propertyRepository.GetById(propertyId);
                if (property is null || property.Status != PropertyStatus.Hidden)
                    return;

                var allDeals = await _repository.GetByProperty(propertyId);
                var hasOtherActiveDeals = allDeals.Any(d =>
                    d.Id != excludeDealId &&
                    d.Status != DealStatus.Cancelled &&
                    d.Status != DealStatus.Completed);

                if (!hasOtherActiveDeals)
                {
                    property.Show();
                    _propertyRepository.Update(property);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось восстановить видимость объекта {PropertyId} после изменения заявки.", propertyId);
            }
        }

        private async Task SyncSaleRequestAfterDealCompletion(Deal completedDeal)
        {
            if (completedDeal.Source == DealSource.Sale || completedDeal.PropertyId == Guid.Empty)
                return;

            var saleRequest = await _repository.GetLatestSaleRequestByProperty(completedDeal.PropertyId);
            if (saleRequest is null || saleRequest.Id == completedDeal.Id)
                return;

            if (saleRequest.Status is DealStatus.Completed or DealStatus.Cancelled)
                return;

            saleRequest.CompleteSaleWorkflow();
            _repository.Update(saleRequest);
        }

        public async Task<Guid> ResolveClientIdByEmail(string email)
        {
            var normalizedEmail = email.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new NotFoundException("Email пользователя не найден в токене.");

            var client = await _clientRepository.GetByEmail(normalizedEmail);
            if (client is not null)
            {
                if (!IsPhoneNumberValid(client.PhoneNumber))
                {
                    var existingIdentityUsers = await _identityAccountManager.GetUsersWithRoles(
                        normalizedEmail,
                        "Client",
                        5,
                        excludeClients: false);

                    var existingIdentityUser = existingIdentityUsers
                        .FirstOrDefault(x => string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

                    var fallbackPhone = BuildSyntheticPhoneNumber(existingIdentityUser?.UserId ?? client.Id);
                    client.Update(fallbackPhone);
                    _clientRepository.Update(client);
                }

                return client.Id;
            }

            var identityUsers = await _identityAccountManager.GetUsersWithRoles(
                normalizedEmail,
                "Client",
                5,
                excludeClients: false);

            var identityUser = identityUsers
                .FirstOrDefault(x => string.Equals(x.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (identityUser is null)
                throw new NotFoundException("Профиль клиента для текущего пользователя не найден.");

            var fullName = BuildClientFullName(identityUser.DisplayName, normalizedEmail);
            var autoCreatedClient = new Client(
                fullName,
                BuildSyntheticPhoneNumber(identityUser.UserId),
                normalizedEmail);

            try
            {
                await _clientRepository.Add(autoCreatedClient);
                return autoCreatedClient.Id;
            }
            catch
            {
                var existingClient = await _clientRepository.GetByEmail(normalizedEmail);
                if (existingClient is not null)
                    return existingClient.Id;

                throw;
            }
        }

        public async Task<Guid> ResolveRealtorIdByEmail(string email)
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

            if (approved is not null)
            {
                var approvedFullName = new FullName(
                    approved.FirstName,
                    approved.LastName,
                    approved.MiddleName);

                var realtorFromApprovedRequest = await CreateRealtorProfileOrGetByPhone(
                    approvedFullName,
                    approved.PhoneNumber);

                return realtorFromApprovedRequest.Id;
            }

            if (!string.IsNullOrWhiteSpace(identityUser.DisplayName))
            {
                var targetName = identityUser.DisplayName.Trim();
                var realtors = await _realtorRepository.GetActive();
                var realtor = realtors.FirstOrDefault(x =>
                    NamesMatch(x.FullName.ToString(), targetName));

                if (realtor is not null)
                    return realtor.Id;
            }

            var autoCreatedFullName = BuildRealtorFullName(identityUser.DisplayName, normalizedEmail);
            var autoCreatedPhone = BuildSyntheticPhoneNumber(identityUser.UserId);
            var autoCreatedRealtor = await CreateRealtorProfileOrGetByPhone(
                autoCreatedFullName,
                autoCreatedPhone);

            return autoCreatedRealtor.Id;
        }

        private async Task EnsureRealtorAccessToDeal(Deal deal, string realtorEmail)
        {
            if (deal.IsIncoming)
                return;

            var realtorId = await ResolveRealtorIdByEmail(realtorEmail);
            if (deal.RealtorId != realtorId)
                throw new NotFoundException("Сделка не найдена.");
        }

        private async Task EnsureAssignedRealtorAccessToDeal(Deal deal, string realtorEmail)
        {
            var realtorId = await ResolveRealtorIdByEmail(realtorEmail);
            if (deal.RealtorId == Guid.Empty || deal.RealtorId != realtorId)
                throw new NotFoundException("Сделка не найдена.");
        }

        private async Task<PropertyOwnerContact> ResolvePropertyOwnerContact(Guid clientId)
        {
            var client = await _clientRepository.GetById(clientId);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            return new PropertyOwnerContact(
                client.FullName.ToString(),
                client.Email,
                client.PhoneNumber);
        }

        private async Task<Guid?> TryResolveRealtorIdByEmail(string email)
        {
            try
            {
                return await ResolveRealtorIdByEmail(email);
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        private async Task ApplyBuyerRequestPriority(Deal deal, Property? property)
        {
            if (deal.Source == DealSource.Sale
                || property?.ResponsibleRealtorId is not Guid responsibleRealtorId)
            {
                return;
            }

            if (!await IsRealtorAvailableForPriority(responsibleRealtorId))
                return;

            deal.SetPriority(
                responsibleRealtorId,
                DateTime.UtcNow.AddHours(BuyerRequestPriorityHours));
        }

        private async Task AssignSalePropertyResponsibleRealtor(Deal deal, Guid realtorId)
        {
            if (deal.Source != DealSource.Sale || deal.PropertyId == Guid.Empty)
                return;

            var property = await _propertyRepository.GetById(deal.PropertyId);
            if (property is null)
                return;

            property.AssignResponsibleRealtor(realtorId);
            _propertyRepository.Update(property);
        }

        private async Task<IReadOnlyList<Deal>> FilterIncomingByPriority(
            IReadOnlyCollection<Deal> deals,
            Guid? realtorId,
            bool includeAssigned = false)
        {
            var result = new List<Deal>(deals.Count);
            foreach (var deal in deals)
            {
                if (!deal.IsIncoming)
                {
                    if (includeAssigned)
                    {
                        result.Add(deal);
                    }

                    continue;
                }

                if (!await HasExclusiveActivePriority(deal))
                {
                    result.Add(deal);
                    continue;
                }

                if (realtorId.HasValue && deal.PriorityRealtorId == realtorId.Value)
                {
                    result.Add(deal);
                }
            }

            return result;
        }

        private async Task EnsurePriorityAllowsAccept(Deal deal, Guid realtorId)
        {
            if (!await HasExclusiveActivePriority(deal))
            {
                if (deal.PriorityRealtorId.HasValue)
                {
                    deal.ClearPriority();
                }

                return;
            }

            if (deal.PriorityRealtorId != realtorId)
            {
                throw new ValidationException("Эта заявка временно доступна только ответственному риелтору объекта.");
            }
        }

        private async Task<bool> TryReleasePriorityOnRejection(Deal deal, Guid realtorId)
        {
            if (!deal.IsIncoming
                || !deal.HasActivePriority(DateTime.UtcNow)
                || deal.PriorityRealtorId != realtorId
                || !await IsRealtorAvailableForPriority(realtorId))
            {
                return false;
            }

            deal.ClearPriority();
            return true;
        }

        private async Task<bool> HasExclusiveActivePriority(Deal deal)
        {
            if (!deal.HasActivePriority(DateTime.UtcNow) || deal.PriorityRealtorId is not Guid priorityRealtorId)
                return false;

            return await IsRealtorAvailableForPriority(priorityRealtorId);
        }

        private async Task<bool> IsRealtorAvailableForPriority(Guid realtorId)
        {
            var realtor = await _realtorRepository.GetById(realtorId);
            if (realtor is null)
                return false;

            var approvedRequests = await _realtorRegistrationRequestRepository.GetApprovedByPhoneNumbers([realtor.PhoneNumber]);
            var approved = approvedRequests
                .Where(x => x.IdentityUserId.HasValue)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefault();

            if (approved?.IdentityUserId is not Guid identityUserId)
            {
                var users = await _identityAccountManager.GetUsersWithRoles(
                    null,
                    "Realtor",
                    500,
                    excludeClients: false);

                var matchedUser = users.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.DisplayName)
                    && NamesMatch(x.DisplayName, realtor.FullName.ToString()));

                return matchedUser is not null && !matchedUser.IsFrozen;
            }

            var identityUser = await _identityAccountManager.GetUserWithRoles(identityUserId);
            return identityUser is not null
                && !identityUser.IsFrozen
                && identityUser.Roles.Any(x => string.Equals(x, "Realtor", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<DealWorkflowResponse>> MapWorkflowResponses(IReadOnlyCollection<Deal> deals)
        {
            if (deals.Count == 0)
                return [];

            var clientIds = deals.Select(x => x.ClientId).Distinct().ToList();
            var clientsById = new Dictionary<Guid, Client>(clientIds.Count);
            foreach (var clientId in clientIds)
            {
                var client = await _clientRepository.GetById(clientId);
                if (client is not null)
                {
                    clientsById[clientId] = client;
                }
            }

            var propertyIds = deals
                .Where(x => x.PropertyId != Guid.Empty)
                .Select(x => x.PropertyId)
                .Distinct()
                .ToList();

            var properties = await _propertyRepository.GetByIds(propertyIds);
            var propertiesById = properties.ToDictionary(x => x.Id);

            var realtorIds = deals
                .Where(x => x.RealtorId != Guid.Empty)
                .Select(x => x.RealtorId)
                .Distinct()
                .ToList();

            var realtorsById = new Dictionary<Guid, Realtor>(realtorIds.Count);
            foreach (var realtorId in realtorIds)
            {
                var realtor = await _realtorRepository.GetById(realtorId);
                if (realtor is not null)
                {
                    realtorsById[realtorId] = realtor;
                }
            }

            var realtorPhones = realtorsById.Values
                .Select(x => x.PhoneNumber)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var approvedRequests = await _realtorRegistrationRequestRepository.GetApprovedByPhoneNumbers(realtorPhones);
            var realtorEmailByPhone = approvedRequests
                .GroupBy(x => x.PhoneNumber)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderByDescending(r => r.CreatedDate).First().Email,
                    StringComparer.OrdinalIgnoreCase);

            return deals
                .OrderByDescending(x => x.CreatedDate)
                .Select(deal =>
                {
                    clientsById.TryGetValue(deal.ClientId, out var client);

                    var propertyId = deal.PropertyId == Guid.Empty ? (Guid?)null : deal.PropertyId;
                    string? propertyTitle = null;
                    if (propertyId.HasValue
                        && propertiesById.TryGetValue(propertyId.Value, out var property))
                    {
                        propertyTitle = property.Title;
                    }

                    Guid? realtorId = deal.RealtorId == Guid.Empty ? null : deal.RealtorId;
                    string? realtorFullName = null;
                    string? realtorPhone = null;
                    string? realtorEmail = null;

                    if (realtorId.HasValue
                        && realtorsById.TryGetValue(realtorId.Value, out var realtor))
                    {
                        realtorFullName = realtor.FullName.ToString();
                        realtorPhone = realtor.PhoneNumber;
                        realtorEmailByPhone.TryGetValue(realtor.PhoneNumber, out realtorEmail);
                    }

                    return new DealWorkflowResponse(
                        deal.Id,
                        deal.ClientId,
                        client?.FullName.ToString() ?? "Клиент не найден",
                        client?.PhoneNumber ?? string.Empty,
                        client?.Email,
                        propertyId,
                        propertyTitle,
                        deal.ClientRequirementId,
                        deal.Source,
                        deal.Status,
                        deal.IsIncoming,
                        realtorId,
                        realtorFullName,
                        realtorPhone,
                        realtorEmail,
                        deal.RequestMessage,
                        deal.AcceptedAtUtc,
                        deal.RejectedAtUtc,
                        deal.PriorityRealtorId,
                        deal.PriorityUntilUtc,
                        deal.CompletedAt,
                        _mapper.Map<List<DealNoteResponse>>(deal.Notes),
                        deal.CreatedDate);
                })
                .ToList();
        }

        private async Task<SaleRequestDetailsResponse> BuildSaleRequestDetailsResponse(
            Deal deal,
            Property? property = null)
        {
            if (deal.PropertyId == Guid.Empty)
                throw new ValidationException("Для заявки не найден объект недвижимости.");

            var resolvedProperty = property;
            if (resolvedProperty is null || resolvedProperty.Id != deal.PropertyId)
            {
                resolvedProperty = await _propertyRepository.GetById(deal.PropertyId);
            }

            if (resolvedProperty is null)
                throw new NotFoundException("Объект недвижимости для заявки не найден.");

            var workflow = (await MapWorkflowResponses([deal]))[0];
            var propertyResponse = _mapper.Map<PropertyResponse>(resolvedProperty);
            var lifecycle = await BuildSaleLifecycleResponse(deal, resolvedProperty);

            return new SaleRequestDetailsResponse(workflow, propertyResponse, lifecycle);
        }

        private async Task<SaleRequestLifecycleResponse> BuildSaleLifecycleResponse(Deal saleDeal, Property property)
        {
            var stage = "Submitted";
            var description = "Заявка отправлена и ожидает назначения риелтора.";

            SaleRequestBuyerDealResponse? buyerDealSummary = null;
            if (saleDeal.PropertyId != Guid.Empty)
            {
                var buyerDeal = await FindLatestBuyerDealForProperty(saleDeal.PropertyId);
                if (buyerDeal is not null)
                {
                    buyerDealSummary = await BuildBuyerDealSummary(buyerDeal);
                }
            }

            if (saleDeal.Status == DealStatus.Cancelled)
            {
                stage = "Cancelled";
                description = "Заявка на продажу отменена.";
            }
            else if (saleDeal.Status == DealStatus.Completed
                     || property.Status == PropertyStatus.Sold
                     || buyerDealSummary?.Status == DealStatus.Completed)
            {
                stage = "DealCompleted";
                description = "Сделка завершена.";
            }
            else if (buyerDealSummary?.Status == DealStatus.InProgress)
            {
                stage = "BuyerFound";
                description = "Покупатель найден, идёт подготовка сделки.";
            }
            else if (saleDeal.Status == DealStatus.InProgress)
            {
                stage = "SearchingBuyer";
                description = "Заявка в работе, выполняется поиск покупателя.";
            }

            return new SaleRequestLifecycleResponse(stage, description, buyerDealSummary);
        }

        private async Task<Deal?> FindLatestBuyerDealForProperty(Guid propertyId)
        {
            var deals = await _repository.GetByProperty(propertyId);
            return deals
                .Where(x => x.Source != DealSource.Sale)
                .Where(x => x.Status is DealStatus.InProgress or DealStatus.Completed)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefault();
        }

        private async Task<SaleRequestBuyerDealResponse?> BuildBuyerDealSummary(Deal buyerDeal)
        {
            var client = await _clientRepository.GetById(buyerDeal.ClientId);
            if (client is null)
                return null;

            return new SaleRequestBuyerDealResponse(
                buyerDeal.Id,
                buyerDeal.ClientId,
                client.FullName.ToString(),
                client.PhoneNumber,
                client.Email,
                buyerDeal.Status,
                buyerDeal.CreatedDate);
        }

        private async Task ApplySaleCriteria(
            Property property,
            IReadOnlyCollection<SaleRequestCriterionRequest>? criteria,
            bool registerNewValues)
        {
            if (criteria is null || criteria.Count == 0)
                return;

            foreach (var criterion in criteria)
            {
                var definition = await _criterionDefinitionRepository.GetByIdForUpdate(criterion.CriterionDefinitionId);
                if (definition is null)
                    throw new NotFoundException("Критерий не найден.");

                var rawValue = BuildCriterionRawValue(criterion);
                var hasExistingValue = property.CriterionValues
                    .Any(x => x.CriterionDefinitionId == definition.Id);

                var value = property.UpsertCriterionValue(definition, rawValue);
                if (registerNewValues && !hasExistingValue)
                {
                    _propertyRepository.AddCriterionValue(value);
                }
            }
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
        private static string BuildCriterionRawValue(SaleRequestCriterionRequest criterion)
        {
            if (criterion.Values is { Count: > 0 })
            {
                var normalized = criterion.Values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (normalized.Count > 0)
                    return JsonSerializer.Serialize(normalized);
            }

            if (!string.IsNullOrWhiteSpace(criterion.Value))
                return criterion.Value.Trim();

            throw new ValidationException("Не передано значение критерия.");
        }

        private static List<DealWorkflowResponse> ApplyWorkflowFilters(
            IReadOnlyCollection<DealWorkflowResponse> source,
            string? search,
            DealStatus? status,
            int limit,
            DealSource? sourceFilter)
        {
            IEnumerable<DealWorkflowResponse> query = source;

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (sourceFilter.HasValue && sourceFilter.Value != DealSource.Undefined)
            {
                query = query.Where(x => x.Source == sourceFilter.Value);
            }

            var normalizedSearch = search?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(x =>
                    ContainsIgnoreCase(x.ClientFullName, normalizedSearch)
                    || ContainsIgnoreCase(x.ClientPhoneNumber, normalizedSearch)
                    || ContainsIgnoreCase(x.ClientEmail, normalizedSearch)
                    || ContainsIgnoreCase(x.PropertyTitle, normalizedSearch)
                    || ContainsIgnoreCase(x.RealtorFullName, normalizedSearch)
                    || ContainsIgnoreCase(x.RealtorPhoneNumber, normalizedSearch)
                    || ContainsIgnoreCase(x.RealtorEmail, normalizedSearch)
                    || ContainsIgnoreCase(x.RequestMessage, normalizedSearch));
            }

            return query
                .OrderByDescending(x => x.CreatedDate)
                .Take(limit)
                .ToList();
        }

        private static List<DealWorkflowResponse> ApplyClientScopeFilter(
            IReadOnlyCollection<DealWorkflowResponse> source,
            string? scope)
        {
            IEnumerable<DealWorkflowResponse> query = source;

            if (string.Equals(scope, "sale", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Source == DealSource.Sale);
            }
            else if (string.Equals(scope, "purchase", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Source != DealSource.Sale);
            }

            return query
                .OrderByDescending(x => x.CreatedDate)
                .ToList();
        }

        private static bool ContainsIgnoreCase(string? value, string search)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private static bool NamesMatch(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                return true;

            var leftTokens = TokenizeName(left);
            var rightTokens = TokenizeName(right);

            if (leftTokens.Count == 0 || rightTokens.Count == 0)
                return false;

            if (leftTokens.SetEquals(rightTokens))
                return true;

            return leftTokens.IsSubsetOf(rightTokens) || rightTokens.IsSubsetOf(leftTokens);
        }

        private static FullName BuildClientFullName(string? displayName, string email)
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
                return new FullName("Client", tokens[0], null);

            var localPart = email.Split('@', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
                return new FullName("Client", localPart, null);

            return new FullName("Client", "Account", null);
        }

        private static string BuildSyntheticPhoneNumber(Guid seed)
        {
            var raw = BitConverter.ToUInt32(seed.ToByteArray(), 0);
            var sixDigits = (raw % 1_000_000U).ToString("D6");
            return $"+37377{sixDigits}";
        }

        private static bool IsPhoneNumberValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var phone = value.Trim();
            var plusCount = phone.Count(c => c == '+');
            if (plusCount > 1 || (plusCount == 1 && !phone.StartsWith("+", StringComparison.Ordinal)))
                return false;

            if (phone.Any(c => !char.IsDigit(c) && c != '+' && c != ' ' && c != '-' && c != '(' && c != ')'))
                return false;

            var digitCount = phone.Count(char.IsDigit);
            return digitCount is >= 7 and <= 15;
        }

        private async Task<Realtor> CreateRealtorProfileOrGetByPhone(
            FullName fullName,
            string phoneNumber)
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

        private async Task EnsureEligibilityForExpensiveDeal(Guid realtorId, Deal deal)
        {
            var eligibility = await _eligibilityService.EvaluateForDeal(realtorId, deal);
            if (eligibility.IsEligible)
                return;

            if (eligibility.IsExpensiveDeal)
            {
                _logger.LogWarning(
                    "Expensive deal assignment blocked. DealId={DealId}, RealtorId={RealtorId}, ReasonCode={ReasonCode}, Reason={Reason}, Cts={ClientTrustScore}",
                    deal.Id,
                    realtorId,
                    eligibility.BlockReasonCode,
                    eligibility.BlockReason,
                    eligibility.ClientTrustScore);

                throw new ValidationException(
                    $"Назначение на дорогую заявку заблокировано: {eligibility.BlockReason}");
            }
        }

        private static HashSet<string> TokenizeName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return [];

            return value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeNameToken)
                .Where(token => token.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeNameToken(string token)
        {
            return new string(token
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        private sealed record PropertyOwnerContact(
            string FullName,
            string? Email,
            string PhoneNumber);
    }
}
