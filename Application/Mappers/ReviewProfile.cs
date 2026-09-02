using AutoMapper;
using Domain.Entities;
using Application.DTOs.Review;

namespace Application.Mappers
{
    public class ReviewProfile : Profile
    {
        public ReviewProfile()
        {
            CreateMap<Review, ReviewResponse>();

            CreateMap<CreateReviewRequest, Review>()
                .ConstructUsing(src =>
                    new Review(
                        src.DealId,
                        src.RealtorId,
                        src.ClientId,
                        src.Score,
                        src.Comment));
        }
    }
}
