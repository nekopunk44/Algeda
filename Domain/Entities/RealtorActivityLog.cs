using Domain.Common;
using Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class RealtorActivityLog : BaseEntity
    {
        public Guid RealtorId { get; private set; }
        public ActivityType Type { get; private set; }
        public int Points { get; private set; }

        public RealtorActivityLog(Guid realtorId, ActivityType type, int points)
        {
            RealtorId = realtorId;
            Type = type;
            Points = points;

            Validate();
        }

        public void Update(ActivityType type, int points)
        {
            Type = type;
            Points = points;

            Validate();
        }

        private void Validate()
        {
            if (RealtorId == Guid.Empty)
                throw new DomainException(ValidationMessages.InvalidGuid(nameof(RealtorId)));

            if (Points <= 0)
                throw new DomainException(ValidationMessages.MustBeGreaterThanZero(nameof(Points)));
        }
    }
}
