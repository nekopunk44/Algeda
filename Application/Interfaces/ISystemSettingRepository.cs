using Domain.Entities;

namespace Application.Interfaces
{
    public interface ISystemSettingRepository : IRepository<SystemSetting>
    {
        string? GetValue(string key);

        void Upsert(string key, string value);
    }
}
