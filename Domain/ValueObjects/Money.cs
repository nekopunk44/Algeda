using Domain.Common;
using Domain.Primitives;

namespace Domain.ValueObjects
{
    public sealed class Money : BaseValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency = "USD")
        {
            if (amount < 0)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Amount)));

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Currency)));

            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            Currency = currency.Trim().ToUpperInvariant();
        }

        public static Money Zero(string currency = "USD") => new(0, currency);

        public Money Add(Money other)
        {
            EnsureSameCurrency(other);
            return new Money(Amount + other.Amount, Currency);
        }

        public Money Subtract(Money other)
        {
            EnsureSameCurrency(other);
            var result = Amount - other.Amount;
            if (result < 0)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Amount)));

            return new Money(result, Currency);
        }

        private void EnsureSameCurrency(Money other)
        {
            if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("Нельзя выполнять операции Money с разной валютой.");
        }

        public override string ToString() => $"{Amount:0.00} {Currency}";
    }
}
