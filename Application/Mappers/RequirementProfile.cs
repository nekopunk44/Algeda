using Application.DTOs.ClientRequirement;
using AutoMapper;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Mappers
{
    public class RequirementProfile : Profile
    {
        public RequirementProfile()
        {
            CreateMap<ClientRequirementCriterion, RequirementCriterionResponse>()
                .ConstructUsing(src => new RequirementCriterionResponse(
                    src.CriterionDefinitionId,
                    src.Priority,
                    src.Value,
                    src.GetValues()));

            CreateMap<RequirementCriterionRequest, ClientRequirementCriterion>()
                .ConstructUsing(src => new ClientRequirementCriterion(
                    src.CriterionDefinitionId,
                    src.Priority,
                    src.Value,
                    src.Values));

            CreateMap<ClientRequirement, RequirementResponse>()
                .ForCtorParam(nameof(RequirementResponse.Latitude),
                    o => o.MapFrom(s => s.TargetLocation.Latitude))
                .ForCtorParam(nameof(RequirementResponse.Longitude),
                    o => o.MapFrom(s => s.TargetLocation.Longitude))
                .ForCtorParam(nameof(RequirementResponse.Criteria),
                    o => o.MapFrom(s => s.Criteria));

            CreateMap<CreateRequirementRequest, ClientRequirement>()
                .ConstructUsing((src, context) =>
                    new ClientRequirement(
                        src.ClientId,
                        src.DesiredType,
                        new Location(src.Latitude, src.Longitude),
                        src.SearchRadiusMeters,
                        src.MinPrice,
                        src.MaxPrice,
                        src.MinArea,
                        src.MinMatchPercentage,
                        src.PriceWeight,
                        src.AreaWeight,
                        src.Criteria is null
                            ? null
                            : src.Criteria
                                .Select(x => context.Mapper.Map<ClientRequirementCriterion>(x))
                                .ToList(),
                        src.DesiredTypes,
                        src.MaxArea,
                        src.AddressQuery,
                        src.IgnoreArea));
        }
    }
}
