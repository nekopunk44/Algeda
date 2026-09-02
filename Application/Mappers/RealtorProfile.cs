using Application.DTOs.Realtor;
using AutoMapper;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Mappers
{
    public class RealtorProfile : Profile
    {
        public RealtorProfile()
        {
            CreateMap<Realtor, RealtorResponse>()
                .ForCtorParam(nameof(RealtorResponse.FirstName),
                    o => o.MapFrom(s => s.FullName.FirstName))
                .ForCtorParam(nameof(RealtorResponse.LastName),
                    o => o.MapFrom(s => s.FullName.LastName))
                .ForCtorParam(nameof(RealtorResponse.MiddleName),
                    o => o.MapFrom(s => s.FullName.MiddleName));

            CreateMap<CreateRealtorRequest, Realtor>()
                .ConstructUsing(src =>
                    new Realtor(
                        new FullName(src.FirstName, src.LastName, src.MiddleName),
                        src.PhoneNumber));
        }
    }
}
