using Application.Interfaces;
using Domain.Entities;

namespace Application.Tests;

internal sealed class InMemorySystemSettingRepository : ISystemSettingRepository
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string? GetValue(string key)
    {
        return _values.GetValueOrDefault(key.Trim());
    }

    public void Upsert(string key, string value)
    {
        _values[key.Trim()] = value;
    }

    public Task<SystemSetting?> GetById(Guid id)
    {
        return Task.FromResult<SystemSetting?>(null);
    }

    public Task<List<SystemSetting>> Get(int limit)
    {
        return Task.FromResult(new List<SystemSetting>());
    }

    public Task<SystemSetting> Add(SystemSetting entity)
    {
        Upsert(entity.Key, entity.Value);
        return Task.FromResult(entity);
    }

    public void Update(SystemSetting entity)
    {
        Upsert(entity.Key, entity.Value);
    }

    public bool Delete(SystemSetting entity)
    {
        return _values.Remove(entity.Key);
    }
}
