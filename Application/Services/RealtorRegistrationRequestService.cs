using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Services
{
    public class RealtorRegistrationRequestService
    {
        private readonly IRealtorRegistrationRequestRepository _repository;
        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly IRealtorRepository _realtorRepository;

        public RealtorRegistrationRequestService(
            IRealtorRegistrationRequestRepository repository,
            IIdentityAccountManager identityAccountManager,
            IRealtorRepository realtorRepository)
        {
            _repository = repository;
            _identityAccountManager = identityAccountManager;
            _realtorRepository = realtorRepository;
        }

        public async Task<List<RealtorRegistrationRequestResponse>> Get(
            RealtorRegistrationRequestStatus? status,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var list = await _repository.GetByStatus(status, safeLimit);
            return list.Select(Map).ToList();
        }

        public async Task<RealtorRegistrationRequestResponse> GetById(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var request = await _repository.GetById(id);
            if (request is null)
                throw new NotFoundException("Заявка риелтора не найдена.");

            return Map(request);
        }

        public async Task<RealtorRegistrationRequestResponse> Approve(
            Guid id,
            string? comment,
            CancellationToken cancellationToken = default)
        {
            var request = await _repository.GetById(id);
            if (request is null)
                throw new NotFoundException("Заявка риелтора не найдена.");

            if (request.Status != RealtorRegistrationRequestStatus.Pending)
                throw new ConflictException("Заявка уже обработана.");

            if (!request.IdentityUserId.HasValue)
                throw new ValidationException("У заявки отсутствует связанная учетная запись.");

            var addRoleResult = await _identityAccountManager.AddToRole(
                request.IdentityUserId.Value,
                "Realtor",
                cancellationToken);

            if (!addRoleResult.Succeeded)
                throw new ValidationException(JoinIdentityErrors(addRoleResult.Errors));

            var existingRealtor = await _realtorRepository.GetByPhone(request.PhoneNumber);
            if (existingRealtor is null)
            {
                var fullName = new FullName(request.FirstName, request.LastName, request.MiddleName);
                var realtor = new Realtor(fullName, request.PhoneNumber);
                await _realtorRepository.Add(realtor);
            }

            request.Approve(comment);
            _repository.Update(request);

            return Map(request);
        }

        public async Task<RealtorRegistrationRequestResponse> Reject(
            Guid id,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            var request = await _repository.GetById(id);
            if (request is null)
                throw new NotFoundException("Заявка риелтора не найдена.");

            if (request.Status != RealtorRegistrationRequestStatus.Pending)
                throw new ConflictException("Заявка уже обработана.");

            request.Reject(reason);
            _repository.Update(request);

            return Map(request);
        }

        private static RealtorRegistrationRequestResponse Map(RealtorRegistrationRequest request)
        {
            return new RealtorRegistrationRequestResponse(
                request.Id,
                request.FirstName,
                request.LastName,
                request.MiddleName,
                request.Email,
                request.PhoneNumber,
                request.IdentityUserId,
                request.Status.ToString(),
                request.ReviewComment,
                request.ReviewedAt,
                request.CreatedDate);
        }

        private static string JoinIdentityErrors(IReadOnlyList<string> errors)
        {
            if (errors.Count == 0)
                return "Не удалось изменить роль пользователя.";

            return string.Join("; ", errors);
        }
    }
}
