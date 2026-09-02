using Application.DTOs.Review;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services
{
    public class ReviewService
    {
        private readonly IReviewRepository _repository;
        private readonly IDealRepository _dealRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IMapper _mapper;

        public ReviewService(
            IReviewRepository repository,
            IDealRepository dealRepository,
            IRealtorRepository realtorRepository,
            IClientRepository clientRepository,
            IMapper mapper)
        {
            _repository = repository;
            _dealRepository = dealRepository;
            _realtorRepository = realtorRepository;
            _clientRepository = clientRepository;
            _mapper = mapper;
        }

        public async Task<ReviewResponse> Create(CreateReviewRequest request)
        {
            var deal = await _dealRepository.GetById(request.DealId);
            if (deal is null)
                throw new NotFoundException("Сделка не найдена.");

            if (deal.Status != DealStatus.Completed)
                throw new ValidationException("Отзыв можно оставить только по завершенной сделке.");

            var realtor = await _realtorRepository.GetById(request.RealtorId);
            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var client = await _clientRepository.GetById(request.ClientId);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            if (deal.RealtorId != request.RealtorId)
                throw new ValidationException("Указанный риелтор не соответствует сделке.");

            if (deal.ClientId != request.ClientId)
                throw new ValidationException("Указанный клиент не соответствует сделке.");

            var existingReview = await _repository.GetByDeal(request.DealId);
            if (existingReview is not null)
                throw new ConflictException("Отзыв по этой сделке уже существует.");

            var review = _mapper.Map<Review>(request);

            await _repository.Add(review);

            return _mapper.Map<ReviewResponse>(review);
        }

        public async Task<ReviewResponse> GetById(Guid id)
        {
            var review = await _repository.GetById(id);

            if (review is null)
                throw new NotFoundException("Отзыв не найден.");

            return _mapper.Map<ReviewResponse>(review);
        }

        public async Task<List<ReviewResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);

            return _mapper.Map<List<ReviewResponse>>(list);
        }

        public async Task<List<ReviewResponse>> GetByRealtor(Guid realtorId)
        {
            var list = await _repository.GetByRealtor(realtorId);

            return _mapper.Map<List<ReviewResponse>>(list);
        }

        public async Task Delete(Guid reviewId)
        {
            var review = await _repository.GetById(reviewId);

            if (review is null)
                throw new NotFoundException("Отзыв не найден.");

            _repository.Delete(review);
        }
    }
}
