using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Review
{
    public record ReviewResponse(
        Guid Id,
        Guid DealId,
        Guid RealtorId,
        Guid ClientId,
        int Score,
        string? Comment,
        DateTime CreatedDate);
}
