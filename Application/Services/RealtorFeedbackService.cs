using Application.DTOs.RealtorEfficiency;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services
{
    public sealed class RealtorFeedbackService : IRealtorFeedbackService
    {
        private static readonly TimeSpan FeedbackWindow = TimeSpan.FromDays(14);

        private readonly IRealtorFeedbackRepository _feedbackRepository;
        private readonly IDealRepository _dealRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IMapper _mapper;

        public RealtorFeedbackService(
            IRealtorFeedbackRepository feedbackRepository,
            IDealRepository dealRepository,
            IPropertyRepository propertyRepository,
            IClientRepository clientRepository,
            IMapper mapper)
        {
            _feedbackRepository = feedbackRepository;
            _dealRepository = dealRepository;
            _propertyRepository = propertyRepository;
            _clientRepository = clientRepository;
            _mapper = mapper;
        }

        public async Task<RealtorFeedbackResponse> SubmitForCurrentClient(
            string clientEmail,
            SubmitDealFeedbackRequest request)
        {
            if (string.IsNullOrWhiteSpace(clientEmail))
                throw new ValidationException("Пользователь не авторизован.");

            var client = await _clientRepository.GetByEmail(clientEmail);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var deal = await _dealRepository.GetById(request.DealId);
            if (deal is null || deal.ClientId != client.Id)
                throw new NotFoundException("Сделка не найдена.");

            if (deal.Status != DealStatus.Completed)
                throw new ValidationException("Оставить отзыв можно только по завершенной сделке.");

            if (deal.CompletedAt is null)
                throw new ValidationException("Не удалось определить дату завершения сделки.");

            var deadline = deal.CompletedAt.Value.Add(FeedbackWindow);
            if (DateTime.UtcNow > deadline)
                throw new ValidationException("Срок отправки отзыва истек.");

            var existing = await _feedbackRepository.GetByDeal(request.DealId);
            if (existing is not null)
                throw new ConflictException("Отзыв по этой сделке уже отправлен.");

            if (deal.RealtorId == Guid.Empty)
                throw new ValidationException("У сделки не назначен риелтор.");

            var expectedFormType = ResolveFormType(deal.Source);
            if (request.FormType != FeedbackFormType.Undefined && request.FormType != expectedFormType)
                throw new ValidationException("Тип отзыва не совпадает с типом сделки.");

            Guid? propertyId = deal.PropertyId == Guid.Empty ? null : deal.PropertyId;
            Guid? propertyResponsibleRealtorId = null;

            if (propertyId.HasValue)
            {
                var property = await _propertyRepository.GetById(propertyId.Value);
                propertyResponsibleRealtorId = property?.ResponsibleRealtorId;
            }

            var isPurchase = expectedFormType == FeedbackFormType.Purchase;
            var feedback = new RealtorFeedback(
                dealId: deal.Id,
                clientId: client.Id,
                serviceRealtorId: deal.RealtorId,
                propertyId: propertyId,
                propertyResponsibleRealtorId: propertyResponsibleRealtorId,
                serviceScore: request.ServiceScore,
                formType: expectedFormType,
                communicationScore: request.CommunicationScore,
                responsivenessScore: request.ResponsivenessScore,
                expertiseScore: request.ExpertiseScore,
                titleAccuracyScore: isPurchase ? request.TitleAccuracyScore : null,
                criteriaAccuracyScore: isPurchase ? request.CriteriaAccuracyScore : null,
                descriptionAccuracyScore: isPurchase ? request.DescriptionAccuracyScore : null,
                photosAccuracyScore: isPurchase ? request.PhotosAccuracyScore : null,
                comment: request.Comment);

            await _feedbackRepository.Add(feedback);
            return _mapper.Map<RealtorFeedbackResponse>(feedback);
        }

        public async Task<DealFeedbackStateResponse> GetStateForCurrentClient(string clientEmail, Guid dealId)
        {
            if (string.IsNullOrWhiteSpace(clientEmail))
                throw new ValidationException("Пользователь не авторизован.");

            var client = await _clientRepository.GetByEmail(clientEmail);
            if (client is null)
                throw new NotFoundException("Клиент не найден.");

            var deal = await _dealRepository.GetById(dealId);
            if (deal is null || deal.ClientId != client.Id)
                throw new NotFoundException("Сделка не найдена.");

            var formType = ResolveFormType(deal.Source);
            var submitted = await _feedbackRepository.GetByDeal(deal.Id);

            if (deal.Status != DealStatus.Completed || !deal.CompletedAt.HasValue)
            {
                return new DealFeedbackStateResponse(
                    DealId: deal.Id,
                    FormType: formType,
                    IsCompleted: false,
                    IsSubmitted: submitted is not null,
                    CanSubmit: false,
                    CompletedAtUtc: deal.CompletedAt,
                    DeadlineUtc: null,
                    DaysRemaining: null,
                    BlockReasonCode: "DEAL_NOT_COMPLETED",
                    BlockReason: "Feedback доступен только по завершенной сделке.");
            }

            var deadline = deal.CompletedAt.Value.Add(FeedbackWindow);
            if (submitted is not null)
            {
                return new DealFeedbackStateResponse(
                    DealId: deal.Id,
                    FormType: formType,
                    IsCompleted: true,
                    IsSubmitted: true,
                    CanSubmit: false,
                    CompletedAtUtc: deal.CompletedAt,
                    DeadlineUtc: deadline,
                    DaysRemaining: BuildDaysRemaining(deadline),
                    BlockReasonCode: "ALREADY_SUBMITTED",
                    BlockReason: "Feedback по этой сделке уже отправлен.");
            }

            if (DateTime.UtcNow > deadline)
            {
                return new DealFeedbackStateResponse(
                    DealId: deal.Id,
                    FormType: formType,
                    IsCompleted: true,
                    IsSubmitted: false,
                    CanSubmit: false,
                    CompletedAtUtc: deal.CompletedAt,
                    DeadlineUtc: deadline,
                    DaysRemaining: 0,
                    BlockReasonCode: "WINDOW_EXPIRED",
                    BlockReason: "Срок отправки feedback истек.");
            }

            return new DealFeedbackStateResponse(
                DealId: deal.Id,
                FormType: formType,
                IsCompleted: true,
                IsSubmitted: false,
                CanSubmit: true,
                CompletedAtUtc: deal.CompletedAt,
                DeadlineUtc: deadline,
                DaysRemaining: BuildDaysRemaining(deadline),
                BlockReasonCode: null,
                BlockReason: null);
        }

        public async Task<RealtorFeedbackSummaryResponse> GetSummaryForRealtor(Guid realtorId, int limit)
        {
            if (realtorId == Guid.Empty)
                throw new ValidationException("Не задан ID риелтора.");

            var safeLimit = Math.Clamp(limit, 1, 100);
            var serviceFeedback = await _feedbackRepository.GetByServiceRealtor(realtorId, safeLimit);
            var propertyFeedback = await _feedbackRepository.GetByPropertyResponsibleRealtor(realtorId, safeLimit);

            var feedbackById = new Dictionary<Guid, RealtorFeedback>();
            foreach (var item in serviceFeedback.Concat(propertyFeedback))
            {
                feedbackById[item.Id] = item;
            }

            var all = feedbackById.Values
                .OrderByDescending(x => x.CreatedDate)
                .ToList();

            var serviceScores = all.Select(x => (double)x.ServiceScore).ToList();
            var communicationScores = all.Where(x => x.CommunicationScore.HasValue).Select(x => (double)x.CommunicationScore!.Value).ToList();
            var responsivenessScores = all.Where(x => x.ResponsivenessScore.HasValue).Select(x => (double)x.ResponsivenessScore!.Value).ToList();
            var expertiseScores = all.Where(x => x.ExpertiseScore.HasValue).Select(x => (double)x.ExpertiseScore!.Value).ToList();

            var purchaseOnly = all.Where(x => x.FormType == FeedbackFormType.Purchase).ToList();
            var titleAccuracyScores = purchaseOnly.Where(x => x.TitleAccuracyScore.HasValue).Select(x => (double)x.TitleAccuracyScore!.Value).ToList();
            var descriptionAccuracyScores = purchaseOnly.Where(x => x.DescriptionAccuracyScore.HasValue).Select(x => (double)x.DescriptionAccuracyScore!.Value).ToList();
            var photosAccuracyScores = purchaseOnly.Where(x => x.PhotosAccuracyScore.HasValue).Select(x => (double)x.PhotosAccuracyScore!.Value).ToList();
            var criteriaAccuracyScores = purchaseOnly.Where(x => x.CriteriaAccuracyScore.HasValue).Select(x => (double)x.CriteriaAccuracyScore!.Value).ToList();

            var recentItems = all
                .Take(safeLimit)
                .Select(x => new RealtorFeedbackSummaryItemResponse(
                    FeedbackId: x.Id,
                    DealId: x.DealId,
                    FormType: x.FormType,
                    IsServiceFeedback: x.ServiceRealtorId == realtorId,
                    IsPropertyFeedback: x.PropertyResponsibleRealtorId == realtorId,
                    ServiceScore: x.ServiceScore,
                    CommunicationScore: x.CommunicationScore,
                    ResponsivenessScore: x.ResponsivenessScore,
                    ExpertiseScore: x.ExpertiseScore,
                    TitleAccuracyScore: x.TitleAccuracyScore,
                    DescriptionAccuracyScore: x.DescriptionAccuracyScore,
                    PhotosAccuracyScore: x.PhotosAccuracyScore,
                    CriteriaAccuracyScore: x.CriteriaAccuracyScore,
                    Comment: x.Comment,
                    CreatedDate: x.CreatedDate))
                .ToList();

            return new RealtorFeedbackSummaryResponse(
                RealtorId: realtorId,
                TotalFeedbackCount: all.Count,
                ServiceFeedbackCount: serviceFeedback.Count,
                PropertyFeedbackCount: propertyFeedback.Count,
                PurchaseFeedbackCount: all.Count(x => x.FormType == FeedbackFormType.Purchase),
                SaleFeedbackCount: all.Count(x => x.FormType == FeedbackFormType.Sale),
                AverageServiceScore: AverageOrNull(serviceScores),
                AverageCommunicationScore: AverageOrNull(communicationScores),
                AverageResponsivenessScore: AverageOrNull(responsivenessScores),
                AverageExpertiseScore: AverageOrNull(expertiseScores),
                AverageTitleAccuracyScore: AverageOrNull(titleAccuracyScores),
                AverageDescriptionAccuracyScore: AverageOrNull(descriptionAccuracyScores),
                AveragePhotosAccuracyScore: AverageOrNull(photosAccuracyScores),
                AverageCriteriaAccuracyScore: AverageOrNull(criteriaAccuracyScores),
                RecentItems: recentItems);
        }

        private static double? AverageOrNull(IReadOnlyCollection<double> source)
        {
            return source.Count == 0
                ? null
                : source.Average();
        }

        private static int BuildDaysRemaining(DateTime deadlineUtc)
        {
            var remaining = deadlineUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return 0;

            return Math.Max(1, (int)Math.Ceiling(remaining.TotalDays));
        }

        private static FeedbackFormType ResolveFormType(DealSource source)
        {
            return source == DealSource.Sale
                ? FeedbackFormType.Sale
                : FeedbackFormType.Purchase;
        }
    }
}
