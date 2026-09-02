using Domain.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.RealtorActivity
{
    public record ActivityLogResponse(
        Guid Id,
        Guid RealtorId,
        ActivityType Type,
        int Points,
        DateTime CreatedDate);
}
