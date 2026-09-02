using Application.DTOs.Realtor;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Application.Services
{
    public class RealtorService
    {
        private readonly IRealtorRepository _repository;
        private readonly IRealtorFeedbackRepository _feedbackRepository;
        private readonly IRealtorLevelCalculationService _levelCalculationService;
        private readonly IMapper _mapper;

        public RealtorService(
            IRealtorRepository repository,
            IRealtorFeedbackRepository feedbackRepository,
            IRealtorLevelCalculationService levelCalculationService,
            IMapper mapper)
        {
            _repository = repository;
            _feedbackRepository = feedbackRepository;
            _levelCalculationService = levelCalculationService;
            _mapper = mapper;
        }

        public async Task<RealtorResponse> Create(CreateRealtorRequest request)
        {
            var existing = await _repository.GetByPhone(request.PhoneNumber);

            if (existing is not null)
                throw new ConflictException("Риелтор с таким номером уже существует.");

            var realtor = _mapper.Map<Realtor>(request);

            await _repository.Add(realtor);

            return await MapResponse(realtor);
        }

        public async Task<RealtorResponse> GetById(Guid id)
        {
            var realtor = await _repository.GetById(id);

            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            return await MapResponse(realtor);
        }

        public async Task<List<RealtorResponse>> Get(int limit)
        {
            var list = await _repository.Get(limit);

            return await MapResponses(list);
        }

        public async Task<List<RealtorResponse>> GetTop(int count)
        {
            var list = await _repository.GetActive();
            var mapped = await MapResponses(list);

            return mapped
                .OrderByDescending(x => x.CurrentKpiScore)
                .ThenByDescending(x => x.AverageRating)
                .Take(Math.Clamp(count, 1, 100))
                .ToList();
        }

        public async Task<RealtorResponse> Update(UpdateRealtorRequest request)
        {
            var realtor = await _repository.GetById(request.Id);

            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            var fullName = new FullName(request.FirstName, request.LastName, request.MiddleName);
            realtor.UpdateProfile(fullName, request.PhoneNumber);

            _repository.Update(realtor);

            return await MapResponse(realtor);
        }

        public async Task<RealtorResponse> UpdateLevelMode(Guid realtorId, UpdateRealtorLevelModeRequest request)
        {
            var realtor = await _repository.GetById(realtorId);

            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            if (request.IsLevelManuallyAssigned)
            {
                var level = request.Level ?? throw new ValidationException("Выберите уровень риелтора.");
                if (level is RealtorLevel.Undefined)
                    throw new ValidationException("Выберите корректный уровень риелтора.");

                realtor.SetManualLevel(level);
            }
            else
            {
                realtor.EnableAutomaticLevel();
            }

            _repository.Update(realtor);

            if (!request.IsLevelManuallyAssigned)
            {
                await _levelCalculationService.Recalculate(realtor.Id);
                realtor = await _repository.GetById(realtorId) ?? realtor;
            }

            return await MapResponse(realtor);
        }

        public async Task Delete(Guid id)
        {
            var realtor = await _repository.GetById(id);

            if (realtor is null)
                throw new NotFoundException("Риелтор не найден.");

            _repository.Delete(realtor);
        }

        private async Task<List<RealtorResponse>> MapResponses(IReadOnlyList<Realtor> realtors)
        {
            var result = new List<RealtorResponse>(realtors.Count);
            foreach (var realtor in realtors)
            {
                result.Add(await MapResponse(realtor));
            }

            return result;
        }

        private async Task<RealtorResponse> MapResponse(Realtor realtor)
        {
            var response = _mapper.Map<RealtorResponse>(realtor);
            var actualRating = await CalculateAverageRating(realtor.Id);
            return response with
            {
                AverageRating = actualRating > 0d
                    ? actualRating
                    : ScoreScaleNormalizer.ToFivePointScale(response.AverageRating),
                CurrentKpiScore = ScoreScaleNormalizer.ToFivePointScale(response.CurrentKpiScore)
            };
        }

        private async Task<double> CalculateAverageRating(Guid realtorId)
        {
            var feedbackByService = await _feedbackRepository.GetByServiceRealtor(realtorId, 500);
            var feedbackByProperty = await _feedbackRepository.GetByPropertyResponsibleRealtor(realtorId, 500);
            var feedbacks = feedbackByService
                .Concat(feedbackByProperty)
                .GroupBy(x => x.Id)
                .Select(x => x.First());

            var values = feedbacks
                .Select(CalculateFeedbackAverage)
                .Where(x => x > 0d)
                .ToList();

            return values.Count == 0
                ? 0d
                : Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
        }

        private static double CalculateFeedbackAverage(RealtorFeedback feedback)
        {
            var values = new List<double> { feedback.ServiceScore };

            if (feedback.CommunicationScore.HasValue)
                values.Add(feedback.CommunicationScore.Value);
            if (feedback.ResponsivenessScore.HasValue)
                values.Add(feedback.ResponsivenessScore.Value);
            if (feedback.ExpertiseScore.HasValue)
                values.Add(feedback.ExpertiseScore.Value);
            if (feedback.TitleAccuracyScore.HasValue)
                values.Add(feedback.TitleAccuracyScore.Value);
            if (feedback.CriteriaAccuracyScore.HasValue)
                values.Add(feedback.CriteriaAccuracyScore.Value);
            if (feedback.DescriptionAccuracyScore.HasValue)
                values.Add(feedback.DescriptionAccuracyScore.Value);
            if (feedback.PhotosAccuracyScore.HasValue)
                values.Add(feedback.PhotosAccuracyScore.Value);

            return values.Count == 0
                ? 0d
                : ScoreScaleNormalizer.ToFivePointScale(values.Average());
        }
    }
}
