using Domain.Common;
using Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.ValueObjects
{
    public record Location
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }

        public Location(double latitude, double longitude)
        {
            if (latitude < -90 || latitude > 90)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Latitude)));

            if (longitude < -180 || longitude > 180)
                throw new DomainException(ValidationMessages.InvalidProperty(nameof(Longitude)));

            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
