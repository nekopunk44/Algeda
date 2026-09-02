using Application.DTOs.Property;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappers
{
    public class PropertyProfile : Profile
    {
        public PropertyProfile()
        {
            CreateMap<Property, PropertyResponse>()
                .ForCtorParam(nameof(PropertyResponse.Latitude),
                    o => o.MapFrom(s => s.Location.Latitude))
                .ForCtorParam(nameof(PropertyResponse.Longitude),
                    o => o.MapFrom(s => s.Location.Longitude))
                .ForCtorParam(nameof(PropertyResponse.PhotoPaths),
                    o => o.MapFrom(s => s.PhotoPaths.ToList()))
                .ForCtorParam(nameof(PropertyResponse.Criteria),
                    o => o.MapFrom(s => s.CriterionValues));

            CreateMap<Property, PropertyManagementResponse>()
                .ForCtorParam(nameof(PropertyManagementResponse.Latitude),
                    o => o.MapFrom(s => s.Location.Latitude))
                .ForCtorParam(nameof(PropertyManagementResponse.Longitude),
                    o => o.MapFrom(s => s.Location.Longitude))
                .ForCtorParam(nameof(PropertyManagementResponse.PhotoPaths),
                    o => o.MapFrom(s => s.PhotoPaths.ToList()));
        }
    }
}
