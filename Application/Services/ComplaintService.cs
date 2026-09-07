using Application.DTOs.Complaint;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Primitives;

namespace Application.Services
{
    public class ComplaintService
    {
        private readonly IComplaintRepository _repository;
        private readonly IClientRepository _clientRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;
        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly IDealRepository _dealRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IMapper _mapper;

        public ComplaintService(
            IComplaintRepository repository,
            IClientRepository clientRepository,
            IRealtorRepository realtorRepository,
            IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository,
            IIdentityAccountManager identityAccountManager,
            IDealRepository dealRepository,
            IPropertyRepository propertyRepository,
            IMapper mapper)
        {
            _repository = repository;
            _clientRepository = clientRepository;
            _realtorRepository = realtorRepository;
            _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
            _identityAccountManager = identityAccountManager;
            _dealRepository = dealRepository;
            _propertyRepository = propertyRepository;
            _mapper = mapper;
        }

        public async Task<ComplaintResponse> CreateForCurrentClient(string clientEmail, CreateComplaintRequest request)
        {
            if (string.IsNullOrWhiteSpace(clientEmail))
                throw new ValidationException("Не удалось определить клиента по email.");

            var client = await _clientRepository.GetByEmail(clientEmail.Trim());
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var requestWithClient = request with { ClientId = client.Id };
            return await CreateInternal(client.Id, requestWithClient);
        }

        public async Task<ComplaintResponse> Create(CreateComplaintRequest request)
        {
            return await CreateInternal(request.ClientId, request);
        }

        private async Task<ComplaintResponse> CreateInternal(Guid currentClientId, CreateComplaintRequest request)
        {
            var client = await _clientRepository.GetById(currentClientId);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var category = request.Category;
            if (category == ComplaintCategory.Undefined)
            {
                category = request.TargetRealtorId.HasValue
                    ? ComplaintCategory.Realtor
                    : ComplaintCategory.Other;
            }

            Guid? targetRealtorId = request.TargetRealtorId;
            Guid? dealId = request.DealId;
            Guid? propertyId = request.PropertyId;

            if (dealId.HasValue)
            {
                var deal = await _dealRepository.GetById(dealId.Value);
                if (deal is null)
                    throw new NotFoundException("Сделка не найдена.");

                if (deal.ClientId != currentClientId)
                    throw new NotFoundException("Сделка не найдена.");

                targetRealtorId ??= deal.RealtorId == Guid.Empty ? null : deal.RealtorId;
                propertyId ??= deal.PropertyId == Guid.Empty ? null : deal.PropertyId;
            }

            if (propertyId.HasValue)
            {
                var property = await _propertyRepository.GetById(propertyId.Value);
                if (property is null)
                    throw new NotFoundException("Объект недвижимости не найден.");

                if (category == ComplaintCategory.PropertyDescriptionMismatch)
                {
                    targetRealtorId ??= property.ResponsibleRealtorId;
                }
            }

            if (category == ComplaintCategory.Realtor && !targetRealtorId.HasValue)
                throw new ValidationException("Для жалобы на риелтора нужно указать риелтора.");

            if (targetRealtorId.HasValue)
            {
                var realtor = await _realtorRepository.GetById(targetRealtorId.Value);
                if (realtor is null)
                    throw new NotFoundException("Риелтор не найден.");
            }

            var uniqueSubject = await BuildUniqueSubject(currentClientId, request.Subject);

            var complaint = new Complaint(
                currentClientId,
                targetRealtorId,
                dealId,
                propertyId,
                category,
                uniqueSubject,
                request.Description);

            await _repository.Add(complaint);

            return _mapper.Map<ComplaintResponse>(complaint);
        }

        private async Task<string> BuildUniqueSubject(Guid clientId, string rawSubject)
        {
            var baseSubject = rawSubject?.Trim() ?? string.Empty;
            if (baseSubject.Length == 0)
            {
                throw new ValidationException("Тема жалобы обязательна.");
            }

            var existingCount = await _repository.CountByClientAndSubjectBase(clientId, baseSubject);
            if (existingCount <= 0)
            {
                return baseSubject;
            }

            return $"{baseSubject} #{existingCount + 1}";
        }

        public async Task<ComplaintResponse> GetById(Guid id)
        {
            var complaint = await _repository.GetById(id);

            if (complaint is null)
                throw new NotFoundException("Жалоба не найдена.");

            return _mapper.Map<ComplaintResponse>(complaint);
        }

        public async Task<List<ComplaintResponse>> Get(
            int limit,
            ComplaintStatus? status = null,
            ComplaintCategory? category = null,
            bool? dealLinked = null)
        {
            var list = await _repository.GetForAdmin(limit, status, category, dealLinked);

            return _mapper.Map<List<ComplaintResponse>>(list);
        }

        public async Task<List<ComplaintResponse>> GetOpen()
        {
            var list = await _repository.GetOpenComplaints();

            return _mapper.Map<List<ComplaintResponse>>(list);
        }

        public async Task<List<ComplaintResponse>> GetForCurrentRealtor(string realtorEmail, int limit)
        {
            var normalizedEmail = realtorEmail.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new ValidationException("Не удалось определить риелтора по email.");

            var approved = await _realtorRegistrationRequestRepository.GetLatestApprovedByEmail(normalizedEmail);
            if (approved is null)
            {
                var identityUser = await _identityAccountManager.GetUserByEmail(normalizedEmail);
                if (identityUser is not null)
                {
                    approved = await _realtorRegistrationRequestRepository
                        .GetLatestApprovedByIdentityUserId(identityUser.UserId);
                }
            }

            if (approved is null)
                throw new NotFoundException("Профиль риелтора не найден.");

            var realtor = await _realtorRepository.GetByPhone(approved.PhoneNumber);
            if (realtor is null)
                throw new NotFoundException("Профиль риелтора не найден.");

            var list = await _repository.GetForRealtor(realtor.Id, limit);
            return _mapper.Map<List<ComplaintResponse>>(list);
        }

        public async Task MarkInProgress(Guid complaintId)
        {
            var complaint = await _repository.GetById(complaintId);

            if (complaint is null)
                throw new NotFoundException("Жалоба не найдена.");

            complaint.MarkAsInProgress();

            _repository.Update(complaint);
        }

        public async Task MarkOpened(Guid complaintId)
        {
            var complaint = await _repository.GetById(complaintId);

            if (complaint is null)
                throw new NotFoundException("Жалоба не найдена.");

            complaint.MarkAsOpened();

            _repository.Update(complaint);
        }

        public async Task<ComplaintResponse> Resolve(ResolveComplaintRequest request)
        {
            var complaint = await _repository.GetById(request.ComplaintId);

            if (complaint is null)
                throw new NotFoundException("Жалоба не найдена.");

            complaint.Resolve(request.Verdict, request.Resolution);

            _repository.Update(complaint);

            return _mapper.Map<ComplaintResponse>(complaint);
        }

        public async Task Delete(Guid id)
        {
            var complaint = await _repository.GetById(id);

            if (complaint is null)
                throw new NotFoundException("Жалоба не найдена.");

            _repository.Delete(complaint);
        }
    }
}
