using Application.DTOs.Complaint;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappers
{
    public class ComplaintProfile : Profile
    {
        public ComplaintProfile()
        {
            CreateMap<Complaint, ComplaintResponse>();

            CreateMap<CreateComplaintRequest, Complaint>()
                .ConstructUsing(src =>
                    new Complaint(
                        src.ClientId,
                        src.TargetRealtorId,
                        src.DealId,
                        src.PropertyId,
                        src.Category,
                        src.Subject,
                        src.Description));
        }
    }
}
