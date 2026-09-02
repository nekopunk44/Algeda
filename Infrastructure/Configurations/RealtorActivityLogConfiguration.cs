using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class RealtorActivityLogConfiguration : IEntityTypeConfiguration<RealtorActivityLog>
    {
        public void Configure(EntityTypeBuilder<RealtorActivityLog> builder)
        {
            builder.ToTable("RealtorActivityLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type)
                .IsRequired();

            builder.Property(x => x.Points)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.RealtorId);
            builder.HasIndex(x => x.CreatedDate);
        }
    }
}
