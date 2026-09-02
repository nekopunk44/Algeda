using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.Review
{
    public record CreateReviewRequest(
        Guid DealId,
        Guid RealtorId,
        Guid ClientId,
        int Score,
        string? Comment);
}
