using Application.DTOs.RealtorActivity;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;

namespace Application.Services
{
    public class RealtorActivityLogService
    {
        private readonly IRealtorActivityLogRepository _repository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IMapper _mapper;

        public RealtorActivityLogService(
            IRealtorActivityLogRepository repository,
            IRealtorRepository realtorRepository,
            IMapper mapper)
        {
            _repository = repository;
            _realtorRepository = realtorRepository;
            _mapper = mapper;
        }

        public async Task<ActivityLogResponse> Create(CreateActivityLogRequest request)
        {
            var realtor = await _realtorRepository.GetById(request.RealtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var entity = _mapper.Map<RealtorActivityLog>(request);

            await _repository.Add(entity);

            return _mapper.Map<ActivityLogResponse>(entity);
        }

        public async Task<ActivityLogResponse> GetById(Guid id)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Запись активности не найдена.");

            return _mapper.Map<ActivityLogResponse>(entity);
        }

        public async Task<List<ActivityLogResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);

            return _mapper.Map<List<ActivityLogResponse>>(list);
        }

        public async Task<List<ActivityLogResponse>> GetByRealtor(Guid realtorId)
        {
            var list = await _repository.GetByRealtor(realtorId);

            return _mapper.Map<List<ActivityLogResponse>>(list);
        }

        public async Task<ActivityLogResponse> Update(Guid id, UpdateActivityLogRequest request)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Запись активности не найдена.");

            entity.Update(request.Type, request.Points);
            _repository.Update(entity);

            return _mapper.Map<ActivityLogResponse>(entity);
        }

        public async Task Delete(Guid id)
        {
            var entity = await _repository.GetById(id);

            if (entity is null)
                throw new NotFoundException("Запись активности не найдена.");

            _repository.Delete(entity);
        }
    }
}
