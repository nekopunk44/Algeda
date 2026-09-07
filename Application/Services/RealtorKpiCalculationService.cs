using Application.DTOs.RealtorKpi;
using Application.Interfaces;

namespace Application.Services
{
    public class RealtorKpiCalculationService
    {
        private const double WeightQuantitative = 0.50;
        private const double WeightQualitative = 0.30;
        private const double WeightDisciplinary = 0.20;

        private const decimal TargetMonthlyCommission = 100_000m;
        private const int TargetMonthlyActivityPoints = 100;
        private const double DefaultRatingWithoutReviews = 4.0;

        private readonly IRealtorRepository _realtorRepository;
        private readonly IDealRepository _dealRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IRealtorActivityLogRepository _activityLogRepository;

        public RealtorKpiCalculationService(
            IRealtorRepository realtorRepository,
            IDealRepository dealRepository,
            IReviewRepository reviewRepository,
            IRealtorActivityLogRepository activityLogRepository)
        {
            _realtorRepository = realtorRepository;
            _dealRepository = dealRepository;
            _reviewRepository = reviewRepository;
            _activityLogRepository = activityLogRepository;
        }

        public async Task<List<RealtorKpiResponse>> Recalculate(RealtorKpiRequest request)
        {
            if (request.Days <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.Days), "Период должен быть больше нуля.");

            var fromUtc = DateTime.UtcNow.AddDays(-request.Days);
            var realtors = await _realtorRepository.GetActive();
            var response = new List<RealtorKpiResponse>(realtors.Count);

            foreach (var realtor in realtors)
            {
                var deals = await _dealRepository.GetCompletedByRealtor(realtor.Id, fromUtc);
                var reviews = await _reviewRepository.GetByRealtor(realtor.Id, fromUtc);
                var activities = await _activityLogRepository.GetByRealtor(realtor.Id, fromUtc);

                var totalCommission = deals.Sum(d => d.CommissionAmount);
                var quantitativeScore = NormalizeTo100(totalCommission, TargetMonthlyCommission);

                var avgRating = reviews.Count == 0
                    ? DefaultRatingWithoutReviews
                    : reviews.Average(r => (double)r.Score);
                var qualitativeScore = Math.Clamp((avgRating / 5.0) * 100.0, 0.0, 100.0);

                var totalActivityPoints = activities.Sum(a => a.Points);
                var disciplinaryScore = NormalizeTo100(totalActivityPoints, TargetMonthlyActivityPoints);

                var finalScore = (WeightQuantitative * quantitativeScore)
                    + (WeightQualitative * qualitativeScore)
                    + (WeightDisciplinary * disciplinaryScore);

                realtor.UpdateKpi(finalScore, avgRating, deals.Count);
                _realtorRepository.Update(realtor);

                response.Add(new RealtorKpiResponse(
                    realtor.Id,
                    finalScore,
                    quantitativeScore,
                    qualitativeScore,
                    disciplinaryScore,
                    avgRating,
                    deals.Count,
                    realtor.Level));
            }

            return response
                .OrderByDescending(x => x.FinalScore)
                .ToList();
        }

        private static double NormalizeTo100(decimal value, decimal target)
        {
            if (target <= 0)
                return 0;

            var ratio = (double)(value / target);
            return Math.Clamp(ratio * 100.0, 0.0, 100.0);
        }

        private static double NormalizeTo100(int value, int target)
        {
            if (target <= 0)
                return 0;

            var ratio = (double)value / target;
            return Math.Clamp(ratio * 100.0, 0.0, 100.0);
        }
    }
}
