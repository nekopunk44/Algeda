using Application.DTOs.Client;
using AutoMapper;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Mappers;

public class ClientProfile : Profile
{
    public ClientProfile()
    {
        CreateMap<Client, ClientResponse>()
            .ForCtorParam(nameof(ClientResponse.FirstName),
                o => o.MapFrom(s => s.FullName.FirstName))
            .ForCtorParam(nameof(ClientResponse.LastName),
                o => o.MapFrom(s => s.FullName.LastName))
            .ForCtorParam(nameof(ClientResponse.MiddleName),
                o => o.MapFrom(s => s.FullName.MiddleName));

        CreateMap<CreateClientRequest, Client>()
            .ConstructUsing(src =>
                new Client(
                    new FullName(src.FirstName, src.LastName, src.MiddleName),
                    src.PhoneNumber,
                    src.Email));
    }
}
