using Application.DTOs.PropertyCriterionDefinition;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;

namespace Application.Services;

public class PropertyCriterionDefinitionService
{
    private readonly IPropertyCriterionDefinitionRepository _repository;
    private readonly IMapper _mapper;

    public PropertyCriterionDefinitionService(
        IPropertyCriterionDefinitionRepository repository,
        IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PropertyCriterionDefinitionResponse> Create(
        CreatePropertyCriterionDefinitionRequest request)
    {
        var existingByCode = await _repository.GetByCode(request.Code);

        if (existingByCode is not null)
            throw new ConflictException("Критерий с таким кодом уже существует.");

        var definition = new PropertyCriterionDefinition(
            request.Code,
            request.DisplayName,
            request.ValueType,
            request.Category,
            request.Description,
            request.IsHidden,
            MapOptions(request.Options));

        await _repository.Add(definition);

        var created = await _repository.GetById(definition.Id);
        return _mapper.Map<PropertyCriterionDefinitionResponse>(created ?? definition);
    }

    public async Task<PropertyCriterionDefinitionResponse> GetById(Guid id)
    {
        var definition = await _repository.GetById(id);

        if (definition is null)
            throw new NotFoundException("Критерий не найден.");

        return _mapper.Map<PropertyCriterionDefinitionResponse>(definition);
    }

    public async Task<List<PropertyCriterionDefinitionResponse>> Get(int limit, bool includeHidden = false)
    {
        var list = await _repository.Get(limit, includeHidden);
        return _mapper.Map<List<PropertyCriterionDefinitionResponse>>(list);
    }

    public async Task<PagedPropertyCriterionDefinitionResponse> GetPage(
        int page,
        int pageSize,
        bool includeHidden,
        string? search,
        string? sort)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _repository.GetPage(
            safePage,
            safePageSize,
            includeHidden,
            search,
            sort);

        return new PagedPropertyCriterionDefinitionResponse(
            safePage,
            safePageSize,
            totalCount,
            _mapper.Map<List<PropertyCriterionDefinitionResponse>>(items));
    }

    public async Task<PropertyCriterionDefinitionResponse> Update(
        Guid id,
        UpdatePropertyCriterionDefinitionRequest request)
    {
        var definition = await _repository.GetByIdForUpdate(id);

        if (definition is null)
            throw new NotFoundException("Критерий не найден.");

        var existingByCode = await _repository.GetByCode(request.Code);
        if (existingByCode is not null && existingByCode.Id != id)
            throw new ConflictException("Критерий с таким кодом уже существует.");

        definition.Update(
            request.Code,
            request.DisplayName,
            request.ValueType,
            request.Category,
            request.Description,
            request.IsHidden,
            MapOptions(request.Options));

        await _repository.SaveChangesAsync();

        var updated = await _repository.GetById(definition.Id);
        return _mapper.Map<PropertyCriterionDefinitionResponse>(updated ?? definition);
    }

    public async Task<PropertyCriterionDefinitionResponse> SetHidden(Guid id, bool isHidden)
    {
        var definition = await _repository.GetByIdForUpdate(id);

        if (definition is null)
            throw new NotFoundException("Критерий не найден.");

        definition.SetHidden(isHidden);
        await _repository.SaveChangesAsync();

        return _mapper.Map<PropertyCriterionDefinitionResponse>(definition);
    }

    public async Task Delete(Guid id)
    {
        var definition = await _repository.GetById(id);

        if (definition is null)
            throw new NotFoundException("Критерий не найден.");

        _repository.Delete(definition);
    }

    private static IEnumerable<(string Value, string Label, int SortOrder)> MapOptions(
        List<PropertyCriterionOptionRequest>? options)
    {
        if (options is null)
            return [];

        return options.Select(x => (x.Value, x.Label, x.SortOrder));
    }
}
