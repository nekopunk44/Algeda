using Domain.Common;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;

namespace Domain.Entities
{
    public class Property : BaseEntity
    {
        public string Title { get; private set; }

        public string Address { get; private set; }

        // Base price used by existing filters/matching/indexes.
        public decimal Price { get; private set; }

        // Original listing price as entered by the user.
        public Money OriginalPrice { get; private set; }

        public decimal OriginalPriceAmount => OriginalPrice.Amount;

        public string OriginalPriceCurrency => OriginalPrice.Currency;

        public double Area { get; private set; }

        public int RoomsCount { get; private set; }

        public Location Location { get; private set; }

        public PropertyType Type { get; private set; }

        public PropertyStatus Status { get; private set; }

        public DateTime? SoldAtUtc { get; private set; }

        public string? OwnerFullName { get; private set; }

        public string? OwnerEmail { get; private set; }

        public string? OwnerPhoneNumber { get; private set; }

        public Guid? OwnerClientId { get; private set; }

        public Guid? ResponsibleRealtorId { get; private set; }

        public List<PropertyCriterionValue> CriterionValues { get; private set; } = [];

        private List<string> _photoPaths = [];

        public IReadOnlyCollection<string> PhotoPaths => _photoPaths.AsReadOnly();

        public string? MainPhotoPath => _photoPaths.Count > 0 ? _photoPaths[0] : null;

        private Property()
        {
            Title = string.Empty;
            Address = string.Empty;
            Price = 1m;
            OriginalPrice = new Money(1m, "USD");
            Area = 1d;
            RoomsCount = 1;
            Location = new Location(0d, 0d);
            Type = PropertyType.Apartment;
            Status = PropertyStatus.Available;
        }

        public Property(
            string title,
            string address,
            decimal price,
            double area,
            int roomsCount,
            Location location,
            PropertyType type,
            string? ownerFullName = null,
            string? ownerEmail = null,
            string? ownerPhoneNumber = null,
            Money? originalPrice = null,
            Guid? responsibleRealtorId = null,
            Guid? ownerClientId = null)
        {
            Title = title;
            Address = address;
            Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
            OriginalPrice = originalPrice ?? new Money(price, "USD");
            Area = area;
            RoomsCount = roomsCount;
            Location = location;
            Type = type;
            Status = PropertyStatus.Available;
            SoldAtUtc = null;
            ResponsibleRealtorId = NormalizeOptionalGuid(responsibleRealtorId);

            UpdateOwnerContact(ownerClientId, ownerFullName, ownerEmail, ownerPhoneNumber);
            Validate();
        }

        public void AssignResponsibleRealtor(Guid realtorId)
        {
            if (realtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(realtorId)));

            ResponsibleRealtorId = realtorId;
        }

        public void ClearResponsibleRealtor()
        {
            ResponsibleRealtorId = null;
        }

        public void MarkAsSold()
        {
            if (Status == PropertyStatus.Sold)
                throw new DomainException(ValidationMessages.AlreadyInState("Объект недвижимости", "Продан"));

            Status = PropertyStatus.Sold;
            SoldAtUtc = DateTime.UtcNow;
        }

        public void Hide()
        {
            if (Status == PropertyStatus.Hidden)
                throw new DomainException(ValidationMessages.AlreadyInState("Объект недвижимости", "Скрыт"));

            if (Status == PropertyStatus.Sold)
                throw new DomainException("Нельзя скрыть объект со статусом \"Продан\".");

            Status = PropertyStatus.Hidden;
        }

        public void Show()
        {
            if (Status == PropertyStatus.Available)
                throw new DomainException(ValidationMessages.AlreadyInState("Объект недвижимости", "Доступен"));

            if (Status != PropertyStatus.Hidden && Status != PropertyStatus.Sold)
                throw new DomainException("Показать можно только скрытый объект.");

            Status = PropertyStatus.Available;
            SoldAtUtc = null;
        }

        public void UpdatePrice(decimal newPrice)
        {
            if (newPrice <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(Price)));

            Price = decimal.Round(newPrice, 2, MidpointRounding.AwayFromZero);
            OriginalPrice = new Money(newPrice, OriginalPrice.Currency);
        }

        public void SetPricing(decimal basePrice, Money originalPrice)
        {
            if (basePrice <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(Price)));

            OriginalPrice = originalPrice ?? throw new DomainException(ValidationMessages.NotNull(nameof(originalPrice)));
            Price = decimal.Round(basePrice, 2, MidpointRounding.AwayFromZero);
        }

        public void UpdateDetails(
            string title,
            string address,
            decimal price,
            double area,
            int roomsCount,
            Location location,
            PropertyType type,
            string? ownerFullName,
            string? ownerEmail,
            string? ownerPhoneNumber,
            Money? originalPrice = null,
            Guid? ownerClientId = null)
        {
            Title = title;
            Address = address;
            Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
            OriginalPrice = originalPrice ?? new Money(price, OriginalPrice.Currency);
            Area = area;
            RoomsCount = roomsCount;
            Location = location;
            Type = type;

            UpdateOwnerContact(ownerClientId, ownerFullName, ownerEmail, ownerPhoneNumber);
            Validate();
        }

        public void UpdateOwnerContact(
            Guid? ownerClientId,
            string? ownerFullName,
            string? ownerEmail,
            string? ownerPhoneNumber)
        {
            OwnerClientId = NormalizeOptionalGuid(ownerClientId);
            OwnerFullName = NormalizeOptional(ownerFullName, 300);
            OwnerEmail = NormalizeOptional(ownerEmail, 256);
            OwnerPhoneNumber = NormalizeOptional(ownerPhoneNumber, 32);
        }

        public PropertyCriterionValue UpsertCriterionValue(
            PropertyCriterionDefinition definition,
            string rawValue)
        {
            if (definition is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(definition)));

            if (definition.Id == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(definition.Id)));

            var existing = CriterionValues
                .FirstOrDefault(x => x.CriterionDefinitionId == definition.Id);

            if (existing is null)
            {
                var newValue = new PropertyCriterionValue(Id, definition, rawValue);
                CriterionValues.Add(newValue);
                return newValue;
            }

            existing.SetValue(definition, rawValue);
            return existing;
        }

        public bool RemoveCriterionValue(Guid criterionDefinitionId)
        {
            var existing = CriterionValues
                .FirstOrDefault(x => x.CriterionDefinitionId == criterionDefinitionId);

            if (existing is null)
                return false;

            CriterionValues.Remove(existing);
            return true;
        }

        public void AddPhoto(string path)
        {
            var normalizedPath = NormalizePhotoPath(path);
            if (_photoPaths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                return;

            if (_photoPaths.Count >= 20)
                throw new DomainException("Максимальное количество фотографий для одного объекта — 20.");

            _photoPaths.Add(normalizedPath);
        }

        public void RemovePhoto(string path)
        {
            var normalizedPath = NormalizePhotoPath(path);
            var existing = _photoPaths
                .FirstOrDefault(x => string.Equals(x, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                _photoPaths.Remove(existing);
            }
        }

        public void ClearPhotos()
        {
            _photoPaths.Clear();
        }

        public void ReplacePhotos(IReadOnlyCollection<string>? photoPaths)
        {
            ClearPhotos();
            if (photoPaths is null || photoPaths.Count == 0)
                return;

            foreach (var photoPath in photoPaths)
            {
                AddPhoto(photoPath);
            }
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(Title))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Title)));

            if (string.IsNullOrWhiteSpace(Address))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Address)));

            if (Price <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(Price)));

            if (OriginalPrice is null)
                throw new DomainException(ValidationMessages.NotNull(nameof(OriginalPrice)));

            if (Area <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(Area)));

            if (RoomsCount <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(RoomsCount)));

            if (Location == null)
                throw new DomainException(ValidationMessages.NotNull(nameof(Location)));
        }

        private static string NormalizePhotoPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new DomainException("Путь к фотографии не может быть пустым.");

            return path.Trim();
        }

        private static string? NormalizeOptional(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static Guid? NormalizeOptionalGuid(Guid? guidValue)
        {
            if (!guidValue.HasValue)
                return null;

            if (guidValue.Value == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(guidValue)));

            return guidValue.Value;
        }
    }
}
