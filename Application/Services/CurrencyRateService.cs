using Application.DTOs.Currency;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services
{
    public class CurrencyRateService
    {
        public const string DefaultBaseCurrencyCode = "USD";

        private readonly ICurrencyRateRepository _repository;

        public CurrencyRateService(ICurrencyRateRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CurrencyRateResponse>> Get(int limit, bool includeInactive)
        {
            var items = await _repository.Get(limit, includeInactive);
            return items.Select(Map).ToList();
        }

        public async Task<CurrencyRateResponse> GetById(Guid id)
        {
            var item = await _repository.GetById(id);
            if (item is null)
                throw new NotFoundException("Валюта не найдена.");

            return Map(item);
        }

        public async Task<CurrencyRateResponse> Create(CreateCurrencyRateRequest request)
        {
            var existing = await _repository.GetByCode(request.Code);
            if (existing is not null)
                throw new ConflictException("Валюта с таким кодом уже существует.");

            var entity = new CurrencyRate(
                request.Code,
                request.Name,
                request.Symbol,
                request.RateToBase,
                request.IsActive);

            await _repository.Add(entity);
            return Map(entity);
        }

        public async Task<CurrencyRateResponse> Update(Guid id, UpdateCurrencyRateRequest request)
        {
            var entity = await _repository.GetByIdForUpdate(id);
            if (entity is null)
                throw new NotFoundException("Валюта не найдена.");

            var existingByCode = await _repository.GetByCode(request.Code);
            if (existingByCode is not null && existingByCode.Id != id)
                throw new ConflictException("Валюта с таким кодом уже существует.");

            entity.UpdateDetails(
                request.Code,
                request.Name,
                request.Symbol,
                request.RateToBase,
                request.IsActive);

            await _repository.SaveChangesAsync();
            return Map(entity);
        }

        public async Task<CurrencyRateResponse> SetActive(Guid id, bool isActive)
        {
            var entity = await _repository.GetByIdForUpdate(id);
            if (entity is null)
                throw new NotFoundException("Валюта не найдена.");

            entity.SetActive(isActive);
            await _repository.SaveChangesAsync();
            return Map(entity);
        }

        public async Task Delete(Guid id)
        {
            var entity = await _repository.GetById(id);
            if (entity is null)
                throw new NotFoundException("Валюта не найдена.");

            if (string.Equals(entity.Code, DefaultBaseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Базовую валюту удалить нельзя.");

            _repository.Delete(entity);
        }

        private static CurrencyRateResponse Map(CurrencyRate entity)
        {
            return new CurrencyRateResponse(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Symbol,
                entity.RateToBase,
                entity.IsActive,
                entity.UpdatedAtUtc,
                entity.CreatedDate);
        }
    }
}
