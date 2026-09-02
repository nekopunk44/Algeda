using Application.Interfaces;
using Infrastructure.Email;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Строка подключения 'DefaultConnection' не найдена.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    connectionString,
                    o => o.UseNetTopologySuite()));

            services
                .AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddErrorDescriber<RussianIdentityErrorDescriber>()
                .AddEntityFrameworkStores<AppDbContext>();

            services
                .AddOptions<IdentitySeedOptions>()
                .Bind(configuration.GetSection(IdentitySeedOptions.SectionName))
                .ValidateOnStart();

            services
                .AddOptions<EmailOptions>()
                .Bind(configuration.GetSection(EmailOptions.SectionName))
                .ValidateOnStart();

            services
                .AddOptions<GmailApiOptions>()
                .Bind(configuration.GetSection(GmailApiOptions.SectionName))
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
            services.AddSingleton<IValidateOptions<GmailApiOptions>, GmailApiOptionsValidator>();
            services.AddScoped<IdentityDataSeeder>();
            services.AddScoped<IIdentityAccountManager, IdentityAccountManager>();
            if (configuration.GetValue<bool>($"{GmailApiOptions.SectionName}:Enabled"))
            {
                services.AddScoped<IEmailSender, GmailApiEmailSender>();
            }
            else
            {
                services.AddScoped<IEmailSender, MailKitEmailSender>();
            }

            services.AddScoped<IPropertyCriteriaDictionarySeeder, PropertyCriteriaDictionarySeeder>();

            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IClientRequirementRepository, ClientRequirementRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
            services.AddScoped<IDealDocumentRepository, DealDocumentRepository>();
            services.AddScoped<IDealDocumentAccessLogRepository, DealDocumentAccessLogRepository>();
            services.AddScoped<IDealRepository, DealRepository>();
            services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
            services.AddScoped<IPropertyRepository, PropertyRepository>();
            services.AddScoped<IPropertyPhotoStorageService, PropertyPhotoStorageService>();
            services.AddScoped<IPropertyMatchNotificationLogRepository, PropertyMatchNotificationLogRepository>();
            services.AddScoped<IPropertyCriterionDefinitionRepository, PropertyCriterionDefinitionRepository>();
            services.AddScoped<IRealtorActivityLogRepository, RealtorActivityLogRepository>();
            services.AddScoped<IRealtorFeedbackRepository, RealtorFeedbackRepository>();
            services.AddScoped<IRealtorRegistrationRequestRepository, RealtorRegistrationRequestRepository>();
            services.AddScoped<IRealtorRepository, RealtorRepository>();
            services.AddScoped<IRealtorScoreSnapshotRepository, RealtorScoreSnapshotRepository>();
            services.AddScoped<IReviewRepository, ReviewRepository>();
            services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();

            return services;
        }
    }
}
