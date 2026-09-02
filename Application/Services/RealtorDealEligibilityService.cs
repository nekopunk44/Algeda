using Application.DTOs.RealtorEfficiency;
using Application.Interfaces;
using Application.Options;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    public sealed class RealtorDealEligibilityService
    {
        private readonly IPropertyRepository _propertyRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorEligibilitySettingsService _eligibilitySettingsService;
        private readonly RealtorEfficiencyOptions _options;

        public RealtorDealEligibilityService(
            IPropertyRepository propertyRepository,
            IRealtorRepository realtorRepository,
            IRealtorEligibilitySettingsService eligibilitySettingsService,
            IOptions<RealtorEfficiencyOptions> options)
        {
            _propertyRepository = propertyRepository;
            _realtorRepository = realtorRepository;
            _eligibilitySettingsService = eligibilitySettingsService;
            _options = options.Value;
        }

        public async Task<RealtorDealEligibilityResponse> EvaluateForDeal(Guid realtorId, Deal deal)
        {
            ArgumentNullException.ThrowIfNull(deal);

            var eligibilitySettings = _eligibilitySettingsService.GetCurrent();
            var tier = await GetMatchedTierForDeal(deal, eligibilitySettings);
            if (!eligibilitySettings.RestrictionsEnabled || tier is null)
            {
                return new RealtorDealEligibilityResponse(
                    IsEligible: true,
                    IsExpensiveDeal: false,
                    ClientTrustScore: null,
                    AdminPerformanceScore: null,
                    EligibilityTierName: null,
                    BlockReasonCode: null,
                    BlockReason: null);
            }

            var realtor = await _realtorRepository.GetById(realtorId);

            var policy = new RealtorDealEligibilityPolicy(_options);
            return policy.Evaluate(
                restrictionsEnabled: true,
                matchedTier: tier,
                eligibilitySettings: eligibilitySettings,
                clientTrustScore: 0,
                adminPerformanceScore: 0,
                criticalComplaintsCount: 0,
                completedDealsHistoryCount: 0,
                realtorLevel: realtor?.Level.ToString());
        }

        private async Task<EligibilityTierOptions?> GetMatchedTierForDeal(Deal deal, EligibilityOptions eligibilitySettings)
        {
            if (deal.PropertyId == Guid.Empty)
                return null;

            var property = await _propertyRepository.GetById(deal.PropertyId);
            if (property is null)
                return null;

            var tiers = (eligibilitySettings.PriceTiers ?? [])
                .OrderBy(x => x.SortOrder)
                .ToList();

            if (tiers.Count == 0)
                return null;

            var price = property.Price;
            return tiers.FirstOrDefault(x => x.Matches(price));
        }

    }
}
