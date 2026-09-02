using Domain.Common;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class ClientRequirement : BaseEntity
    {
        private const int MaxAddressQueryLength = 500;

        public Guid ClientId { get; private set; }
        public bool IsActive { get; private set; }

        // Legacy compatibility field. New filtering uses DesiredTypes.
        public PropertyType DesiredType { get; private set; }
        public List<PropertyType> DesiredTypes { get; private set; } = [];

        public Location TargetLocation { get; private set; }
        public double SearchRadiusMeters { get; private set; }
        public bool IgnoreArea { get; private set; }

        public decimal MinPrice { get; private set; }
        public decimal MaxPrice { get; private set; }
        public double MinArea { get; private set; }
        public double? MaxArea { get; private set; }
        public string? AddressQuery { get; private set; }

        // Legacy compatibility fields. They are no longer exposed in client UI.
        public double PriceWeight { get; private set; }
        public double AreaWeight { get; private set; }
        public double MinMatchPercentage { get; private set; }

        public List<ClientRequirementCriterion> Criteria { get; private set; } = [];

        public ClientRequirement(
            Guid clientId,
            PropertyType desiredType,
            Location targetLocation,
            double searchRadiusMeters,
            decimal minPrice,
            decimal maxPrice,
            double minArea,
            double minMatchPercentage,
            double priceWeight = 0.6,
            double areaWeight = 0.4,
            IEnumerable<ClientRequirementCriterion>? criteria = null,
            IEnumerable<PropertyType>? desiredTypes = null,
            double? maxArea = null,
            string? addressQuery = null,
            bool ignoreArea = false)
        {
            ClientId = clientId;
            TargetLocation = targetLocation;
            SearchRadiusMeters = searchRadiusMeters;
            IgnoreArea = ignoreArea;
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            MinArea = minArea;
            MaxArea = maxArea;
            AddressQuery = NormalizeAddressQuery(addressQuery);
            PriceWeight = priceWeight;
            AreaWeight = areaWeight;
            MinMatchPercentage = minMatchPercentage;
            IsActive = true;

            SetDesiredTypes(desiredType, desiredTypes);
            ReplaceCriteria(criteria ?? []);
            Validate();
        }

        private ClientRequirement()
        {
            TargetLocation = null!;
        }

        public void Update(
            PropertyType desiredType,
            Location targetLocation,
            double searchRadiusMeters,
            decimal minPrice,
            decimal maxPrice,
            double minArea,
            double minMatchPercentage,
            double priceWeight = 0.6,
            double areaWeight = 0.4,
            IEnumerable<ClientRequirementCriterion>? criteria = null,
            IEnumerable<PropertyType>? desiredTypes = null,
            double? maxArea = null,
            string? addressQuery = null,
            bool ignoreArea = false)
        {
            TargetLocation = targetLocation;
            SearchRadiusMeters = searchRadiusMeters;
            IgnoreArea = ignoreArea;
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            MinArea = minArea;
            MaxArea = maxArea;
            AddressQuery = NormalizeAddressQuery(addressQuery);
            PriceWeight = priceWeight;
            AreaWeight = areaWeight;
            MinMatchPercentage = minMatchPercentage;

            SetDesiredTypes(desiredType, desiredTypes);

            // Backward compatibility: if criteria is absent, keep current criteria set.
            if (criteria is not null)
            {
                ReplaceCriteria(criteria);
            }

            Validate();
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        private void SetDesiredTypes(
            PropertyType desiredType,
            IEnumerable<PropertyType>? desiredTypes)
        {
            var normalizedTypes = desiredTypes?
                .Distinct()
                .ToList()
                ?? [];

            if (normalizedTypes.Count == 0 && desiredType != PropertyType.Undefined)
            {
                normalizedTypes.Add(desiredType);
            }

            DesiredTypes = normalizedTypes;
            DesiredType = DesiredTypes.Count > 0
                ? DesiredTypes[0]
                : PropertyType.Undefined;
        }

        private void ReplaceCriteria(IEnumerable<ClientRequirementCriterion> criteria)
        {
            var incoming = criteria.ToList();

            var duplicate = incoming
                .GroupBy(x => x.CriterionDefinitionId)
                .FirstOrDefault(x => x.Count() > 1);

            if (duplicate is not null)
                throw new DomainException("Дублирующиеся критерии требования не допускаются.");

            Criteria.Clear();

            foreach (var incomingItem in incoming)
            {
                if (incomingItem is null)
                    throw new DomainException(ValidationMessages.NotNull(nameof(incomingItem)));

                incomingItem.AttachToRequirement(Id);
                Criteria.Add(incomingItem);
            }
        }

        private void Validate()
        {
            if (ClientId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(ClientId)));

            if (TargetLocation is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(TargetLocation)));

            if (SearchRadiusMeters <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(SearchRadiusMeters)));

            if (DesiredTypes.Any(x => x == PropertyType.Undefined))
                throw new DomainException("Список типов недвижимости не может содержать Undefined.");

            if (DesiredTypes.Count != DesiredTypes.Distinct().Count())
                throw new DomainException("Список типов недвижимости содержит дубликаты.");

            if (MinPrice < 0 || MaxPrice <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero("Цена"));

            if (MinPrice > MaxPrice)
                throw new DomainException(ValidationMessages.InvalidProperty("Диапазон цен"));

            if (MinArea <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(MinArea)));

            if (MaxArea.HasValue && MaxArea.Value < MinArea)
                throw new DomainException("Максимальная площадь не может быть меньше минимальной.");

            if (PriceWeight <= 0 || PriceWeight > 1)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(PriceWeight)));

            if (AreaWeight <= 0 || AreaWeight > 1)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(AreaWeight)));

            const double epsilon = 0.000001;
            if (Math.Abs((PriceWeight + AreaWeight) - 1.0) > epsilon)
                throw new DomainException("Сумма весов PriceWeight и AreaWeight должна быть равна 1.");

            if (MinMatchPercentage <= 0 || MinMatchPercentage > 1)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(MinMatchPercentage)));
        }

        private static string? NormalizeAddressQuery(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            return normalized.Length <= MaxAddressQueryLength
                ? normalized
                : normalized[..MaxAddressQueryLength];
        }
    }
}
