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
            var setting = new SystemSetting(key, value);

            _context.Database.ExecuteSqlInterpolated($"""
                INSERT INTO "SystemSettings" ("Id", "Key", "Value", "UpdatedAtUtc", "CreatedDate")
                VALUES ({setting.Id}, {setting.Key}, {setting.Value}, {setting.UpdatedAtUtc}, {setting.CreatedDate})
                ON CONFLICT ("Key") DO UPDATE
                SET "Value" = EXCLUDED."Value",
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";
                """);
        }
    }
}
