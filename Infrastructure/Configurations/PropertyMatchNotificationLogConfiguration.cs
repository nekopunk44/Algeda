using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class PropertyMatchNotificationLogConfiguration : IEntityTypeConfiguration<PropertyMatchNotificationLog>
    {
        public void Configure(EntityTypeBuilder<PropertyMatchNotificationLog> builder)
        {
            builder.ToTable("PropertyMatchNotificationLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.RequirementId)
                .IsRequired();

            builder.Property(x => x.PropertyId)
                .IsRequired();

            builder.Property(x => x.NotificationType)
                .IsRequired();

            builder.Property(x => x.SentAt)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => new { x.RequirementId, x.PropertyId, x.NotificationType })
                .IsUnique();

            builder.HasIndex(x => new { x.RequirementId, x.NotificationType });
            builder.HasIndex(x => new { x.PropertyId, x.NotificationType });
        }
    }
}
