using Application.DTOs.Deal;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappers
{
    public class DealProfile : Profile
    {
        public DealProfile()
        {
            CreateMap<Deal, DealResponse>();
            CreateMap<DealNote, DealNoteResponse>();

            CreateMap<CreateDealRequest, Deal>()
                .ConstructUsing(src =>
                    new Deal(
                        src.PropertyId,
                        src.ClientId,
                        src.RealtorId));
        }
    }
}
