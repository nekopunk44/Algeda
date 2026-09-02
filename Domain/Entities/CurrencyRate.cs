using Domain.Common;
using Domain.Primitives;

namespace Domain.Entities
{
    public class CurrencyRate : BaseEntity
    {
        public string Code { get; private set; }

        public string Name { get; private set; }

        public string Symbol { get; private set; }

        // How many base-currency units are in 1 unit of this currency.
        public decimal RateToBase { get; private set; }

        public bool IsActive { get; private set; }

        public DateTime UpdatedAtUtc { get; private set; }

        public CurrencyRate(
            string code,
            string name,
            string symbol,
            decimal rateToBase,
            bool isActive = true)
        {
            Code = NormalizeCode(code);
            Name = NormalizeName(name);
            Symbol = NormalizeSymbol(symbol);
            RateToBase = NormalizeRate(rateToBase);
            IsActive = isActive;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        private CurrencyRate()
        {
            Code = "USD";
            Name = "US Dollar";
            Symbol = "$";
            RateToBase = 1m;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void UpdateDetails(string code, string name, string symbol, decimal rateToBase, bool isActive)
        {
            Code = NormalizeCode(code);
            Name = NormalizeName(name);
            Symbol = NormalizeSymbol(symbol);
            RateToBase = NormalizeRate(rateToBase);
            IsActive = isActive;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
            UpdatedAtUtc = DateTime.UtcNow;
        }

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Code)));

            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length != 3 || normalized.Any(c => !char.IsLetter(c)))
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Code)));

            return normalized;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Name)));

            var normalized = name.Trim();
            if (normalized.Length > 100)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Name)));

            return normalized;
        }

        private static string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(Symbol)));

            var normalized = symbol.Trim();
            if (normalized.Length > 10)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Symbol)));

            return normalized;
        }

        private static decimal NormalizeRate(decimal rate)
        {
            if (rate <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(RateToBase)));

            return decimal.Round(rate, 6, MidpointRounding.AwayFromZero);
        }
    }
}
