using Domain.Entities;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<ClientRequirement> ClientRequirements => Set<ClientRequirement>();
        public DbSet<ClientRequirementCriterion> ClientRequirementCriteria => Set<ClientRequirementCriterion>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<Deal> Deals => Set<Deal>();
        public DbSet<DealDocument> DealDocuments => Set<DealDocument>();
        public DbSet<DealDocumentAccessLog> DealDocumentAccessLogs => Set<DealDocumentAccessLog>();
        public DbSet<DealNote> DealNotes => Set<DealNote>();
        public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<PropertyPhotoBlob> PropertyPhotoBlobs => Set<PropertyPhotoBlob>();
        public DbSet<PropertyMatchNotificationLog> PropertyMatchNotificationLogs => Set<PropertyMatchNotificationLog>();
        public DbSet<PropertyCriterionDefinition> PropertyCriterionDefinitions => Set<PropertyCriterionDefinition>();
        public DbSet<PropertyCriterionOption> PropertyCriterionOptions => Set<PropertyCriterionOption>();
        public DbSet<PropertyCriterionValue> PropertyCriterionValues => Set<PropertyCriterionValue>();
        public DbSet<RealtorRegistrationRequest> RealtorRegistrationRequests => Set<RealtorRegistrationRequest>();
        public DbSet<Realtor> Realtors => Set<Realtor>();
        public DbSet<RealtorActivityLog> RealtorActivityLogs => Set<RealtorActivityLog>();
        public DbSet<RealtorFeedback> RealtorFeedbacks => Set<RealtorFeedback>();
        public DbSet<RealtorScoreSnapshot> RealtorScoreSnapshots => Set<RealtorScoreSnapshot>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
