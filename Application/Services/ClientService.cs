using Application.DTOs.Client;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;

namespace Application.Services;

public class ClientService
{
    private readonly IClientRepository _repository;
    private readonly IMapper _mapper;

    public ClientService(IClientRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ClientResponse> Create(CreateClientRequest request)
    {
        var existing = await _repository.GetByPhone(request.PhoneNumber);

        if (existing is not null)
            throw new ConflictException("Клиент с таким номером уже существует.");

        var client = _mapper.Map<Client>(request);

        await _repository.Add(client);

        return _mapper.Map<ClientResponse>(client);
    }

    public async Task<ClientResponse> GetById(Guid id)
    {
        var client = await _repository.GetById(id);

        if (client is null)
            throw new NotFoundException("Клиент не найден.");

        return _mapper.Map<ClientResponse>(client);
    }

    public async Task<List<ClientResponse>> Get(int limit)
    {
        var clients = await _repository.Get(limit);

        return _mapper.Map<List<ClientResponse>>(clients);
    }

    public async Task<ClientResponse> Update(UpdateClientRequest request)
    {
        var client = await _repository.GetById(request.Id);

        if (client is null)
            throw new NotFoundException("Клиент не найден.");

        client.Update(request.PhoneNumber);

        _repository.Update(client);

        return _mapper.Map<ClientResponse>(client);
    }

    public async Task Delete(Guid id)
    {
        var client = await _repository.GetById(id);

        if (client is null)
            throw new NotFoundException("Клиент не найден.");

        _repository.Delete(client);
    }
}
