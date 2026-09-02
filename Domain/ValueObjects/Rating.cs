using Domain.Common;
using Domain.Primitives;

namespace Domain.ValueObjects
{
    public sealed class Rating : BaseValueObject
    {
        public int Value { get; }

        public Rating(int value)
        {
            if (value < 1 || value > 5)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Value)));

            Value = value;
        }

        public static implicit operator int(Rating rating) => rating.Value;
    }
}
