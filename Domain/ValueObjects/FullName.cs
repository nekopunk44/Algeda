using Domain.Common;
using Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.ValueObjects
{
    public class FullName : BaseValueObject
    {
        public string FirstName { get; }
        public string LastName { get; }
        public string? MiddleName { get; }

        public FullName(string firstName, string lastName, string? middleName)
        {
            FirstName = firstName;
            LastName = lastName;
            MiddleName = middleName;

            Validate();
        }

        private void Validate()
        {
            if (FirstName == null)
                throw new DomainException(ValidationMessages.NotNull(nameof(FirstName)));

            if (string.IsNullOrWhiteSpace(FirstName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(FirstName)));

            if (LastName == null)
                throw new DomainException(ValidationMessages.NotNull(nameof(LastName)));

            if (string.IsNullOrWhiteSpace(LastName))
                throw new DomainException(ValidationMessages.NotEmpty(nameof(LastName)));
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(MiddleName)
                ? $"{LastName} {FirstName}"
                : $"{LastName} {FirstName} {MiddleName}";
        }
    }
}
