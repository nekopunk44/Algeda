using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class SystemSettingRepository : Repository<SystemSetting>, ISystemSettingRepository
    {
        private readonly AppDbContext _context;

        public SystemSettingRepository(AppDbContext context)
            : base(context)
        {
            _context = context;
        }

        public string? GetValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            return _context.SystemSettings
                .AsNoTracking()
                .Where(x => x.Key == key.Trim())
                .Select(x => x.Value)
                .FirstOrDefault();
        }

        public void Upsert(string key, string value)
        {
            var normalizedKey = key.Trim();
            var setting = _context.SystemSettings
                .FirstOrDefault(x => x.Key == normalizedKey);

            if (setting is null)
            {
                _context.SystemSettings.Add(new SystemSetting(normalizedKey, value));
            }
            else
            {
                setting.UpdateValue(value);
            }

            _context.SaveChanges();
        }
    }
}
