using System.Text.Json;
using Application.DTOs.PropertyMatching;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace Application.Services
{
    public class PropertyMatchingService
    {
        private const double HardFiltersScore = 1.0;
        private const double ImportantCriteriaWeight = 0.70;
        private const double NiceToHaveCriteriaWeight = 0.30;

        private readonly IPropertyRepository _propertyRepository;
        private readonly IClientRequirementRepository _requirementRepository;

        public PropertyMatchingService(
            IPropertyRepository propertyRepository,
            IClientRequirementRepository requirementRepository)
        {
            _propertyRepository = propertyRepository;
            _requirementRepository = requirementRepository;
        }

        public async Task<List<MatchedPropertyResponse>> FindMatches(PropertyMatchingRequest request)
        {
            var requirement = await _requirementRepository.GetById(request.RequirementId);

            if (requirement is null)
                throw new NotFoundException("Требование клиента не найдено.");

            if (!requirement.IsActive)
                return [];

            return await FindMatchesCore(requirement, request.Limit);
        }

        public async Task<List<MatchedPropertyResponse>> FindMatchesPreview(PropertyMatchingPreviewRequest request)
        {
            var desiredTypes = request.DesiredTypes?
                .Where(x => x != PropertyType.Undefined)
                .Distinct()
                .ToList();

            var criteria = request.Criteria?
                .Where(x => x.CriterionDefinitionId != Guid.Empty && x.Priority != RequirementCriterionPriority.Undefined)
                .Select(x => new ClientRequirementCriterion(
                    x.CriterionDefinitionId,
                    x.Priority,
                    string.IsNullOrWhiteSpace(x.Value) ? null : x.Value.Trim(),
                    x.Values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList()))
                .ToList();

            var requirement = new ClientRequirement(
                Guid.NewGuid(),
                request.DesiredType,
                new Location(request.Latitude, request.Longitude),
                request.SearchRadiusMeters,
                request.MinPrice,
                request.MaxPrice,
                request.MinArea,
                request.MinMatchPercentage,
                priceWeight: 0.5,
                areaWeight: 0.5,
                criteria: criteria,
                desiredTypes: desiredTypes,
                maxArea: request.MaxArea,
                addressQuery: request.AddressQuery,
                ignoreArea: request.IgnoreArea);

            return await FindMatchesCore(requirement, request.Limit);
        }

        public async Task<List<Guid>> FindSubscribedClientsForProperty(Guid propertyId, int requirementsLimit = 500)
        {
            var matches = await FindRequirementMatchesForProperty(propertyId, requirementsLimit);

            return matches
                .Select(x => x.ClientId)
                .Distinct()
                .ToList();
        }

        public async Task<List<PropertySubscriberMatch>> FindRequirementMatchesForProperty(
            Guid propertyId,
            int requirementsLimit = 500)
        {
            var property = await _propertyRepository.GetById(propertyId);

            if (property is null)
                throw new NotFoundException("Объект не найден.");

            var requirements = await _requirementRepository.GetActive(requirementsLimit);
            var matches = new List<PropertySubscriberMatch>();

            foreach (var requirement in requirements)
            {
                if (!requirement.IsActive)
                    continue;

                if (!MatchesHardFilters(property, requirement, out var distanceMeters))
                    continue;

                var score = CalculateScore(property, requirement);
                if (score.FinalScore < requirement.MinMatchPercentage)
                    continue;

                matches.Add(new PropertySubscriberMatch(
                    requirement.Id,
                    requirement.ClientId,
                    property.Id,
                    property.Title,
                    property.Price,
                    property.Area,
                    property.Address,
                    score.FinalScore,
                    distanceMeters));
            }

            return matches;
        }

        private async Task<List<MatchedPropertyResponse>> FindMatchesCore(ClientRequirement requirement, int limit)
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var candidatesTake = Math.Max(safeLimit * 4, 40);
            var candidates = await _propertyRepository.GetCandidatesForRequirement(
                requirement,
                candidatesTake);

            var scored = new List<(Property Property, double DistanceMeters, ScoreResult Score)>();

            foreach (var candidate in candidates)
            {
                if (!MatchesHardFilters(candidate.Property, requirement, out var distanceMeters))
                {
                    continue;
                }

                var score = CalculateScore(candidate.Property, requirement);
                if (score.FinalScore < requirement.MinMatchPercentage)
                {
                    continue;
                }

                scored.Add((candidate.Property, distanceMeters, score));
            }

            return scored
                .OrderByDescending(x => x.Score.FinalScore)
                .ThenBy(x => x.DistanceMeters)
                .Take(safeLimit)
                .Select(x => new MatchedPropertyResponse(
                    x.Property.Id,
                    x.Property.Title,
                    x.Property.Address,
                    x.Property.Price,
                    x.Property.OriginalPriceAmount,
                    x.Property.OriginalPriceCurrency,
                    x.Property.Area,
                    x.Property.RoomsCount,
                    x.Property.Location.Latitude,
                    x.Property.Location.Longitude,
                    x.DistanceMeters,
                    x.Score.FinalScore,
                    new MatchedPropertyScoreBreakdownResponse(
                        x.Score.BaseScore,
                        x.Score.CriteriaScore,
                        x.Score.PassedMustHave)))
                .ToList();
        }

        private static bool MatchesHardFilters(
            Property property,
            ClientRequirement requirement,
            out double distanceMeters)
        {
            distanceMeters = CalculateDistanceMeters(requirement.TargetLocation, property.Location);

            var desiredTypes = GetDesiredTypes(requirement);
            if (desiredTypes.Count > 0 && !desiredTypes.Contains(property.Type))
                return false;

            if (property.Price < requirement.MinPrice || property.Price > requirement.MaxPrice)
                return false;

            if (property.Area < requirement.MinArea)
                return false;

            if (requirement.MaxArea.HasValue && property.Area > requirement.MaxArea.Value)
                return false;

            if (!string.IsNullOrWhiteSpace(requirement.AddressQuery)
                && !property.Address.Contains(requirement.AddressQuery.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            if (!requirement.IgnoreArea && distanceMeters > requirement.SearchRadiusMeters)
                return false;

            return true;
        }

        private static HashSet<PropertyType> GetDesiredTypes(ClientRequirement requirement)
        {
            var types = requirement.DesiredTypes
                .Where(x => x != PropertyType.Undefined)
                .ToHashSet();

            if (types.Count == 0 && requirement.DesiredType != PropertyType.Undefined)
            {
                types.Add(requirement.DesiredType);
            }

            return types;
        }

        private static ScoreResult CalculateScore(Property property, ClientRequirement requirement)
        {
            var propertyValues = property.CriterionValues
                .GroupBy(x => x.CriterionDefinitionId)
                .ToDictionary(
                    x => x.Key,
                    x => NormalizePropertyValues(x.First().Value));

            var mustHave = requirement.Criteria.Where(x => x.Priority == RequirementCriterionPriority.MustHave).ToList();
            var passedMustHave = mustHave.All(x => IsCriterionMatched(x, propertyValues));

            if (!passedMustHave)
            {
                return new ScoreResult(0, 0, 0, false);
            }

            const double baseFilterWeight = 40.0;
            const double mustHaveWeight = 10.0;
            const double importantWeight = 5.0;
            const double niceToHaveWeight = 2.0;

            double totalPossibleWeight = baseFilterWeight + (mustHave.Count * mustHaveWeight);
            double earnedWeight = baseFilterWeight + (mustHave.Count * mustHaveWeight);

            double possibleCriteriaWeight = 0.0;
            double earnedCriteriaWeight = 0.0;

            foreach (var criterion in requirement.Criteria)
            {
                if (criterion.Priority == RequirementCriterionPriority.MustHave)
                    continue;

                var matched = IsCriterionMatched(criterion, propertyValues);
                var weight = criterion.Priority == RequirementCriterionPriority.Important
                    ? importantWeight
                    : niceToHaveWeight;

                totalPossibleWeight += weight;
                possibleCriteriaWeight += weight;

                if (matched)
                {
                    earnedWeight += weight;
                    earnedCriteriaWeight += weight;
                }
            }

            var finalScore = totalPossibleWeight > 0 ? earnedWeight / totalPossibleWeight : 1.0;
            var criteriaOnlyScore = possibleCriteriaWeight > 0 ? earnedCriteriaWeight / possibleCriteriaWeight : 0.0;

            return new ScoreResult(
                Math.Clamp(finalScore, 0.0, 1.0),
                HardFiltersScore,
                Math.Clamp(criteriaOnlyScore, 0.0, 1.0),
                true);
        }

        private static double CalculateMatchRate(
            IReadOnlyCollection<ClientRequirementCriterion> criteria,
            IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> propertyValues)
        {
            if (criteria.Count == 0)
                return 0;

            var matchedCount = criteria.Count(x => IsCriterionMatched(x, propertyValues));
            return (double)matchedCount / criteria.Count;
        }

        private static bool IsCriterionMatched(
            ClientRequirementCriterion criterion,
            IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> propertyValues)
        {
            if (!propertyValues.TryGetValue(criterion.CriterionDefinitionId, out var propertyCriterionValues))
                return false;

            var requiredValues = criterion.GetValues()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (requiredValues.Count == 0)
                return false;

            return propertyCriterionValues.Any(requiredValues.Contains);
        }

        private static IReadOnlyCollection<string> NormalizePropertyValues(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return [];

            var trimmed = rawValue.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                var values = JsonSerializer.Deserialize<List<string>>(trimmed) ?? [];
                return values
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return [trimmed];
        }

        private static double CalculateDistanceMeters(Location from, Location to)
        {
            const double earthRadius = 6371000;

            var latFrom = DegreesToRadians(from.Latitude);
            var lonFrom = DegreesToRadians(from.Longitude);
            var latTo = DegreesToRadians(to.Latitude);
            var lonTo = DegreesToRadians(to.Longitude);

            var dLat = latTo - latFrom;
            var dLon = lonTo - lonFrom;

            var a = Math.Pow(Math.Sin(dLat / 2), 2)
                    + Math.Cos(latFrom) * Math.Cos(latTo) * Math.Pow(Math.Sin(dLon / 2), 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadius * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

        private readonly record struct ScoreResult(
            double FinalScore,
            double BaseScore,
            double CriteriaScore,
            bool PassedMustHave);
    }
}
