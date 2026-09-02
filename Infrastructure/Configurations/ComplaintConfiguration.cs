using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
    {
        public void Configure(EntityTypeBuilder<Complaint> builder)
        {
            builder.ToTable("Complaints");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Subject)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(4000)
                .IsRequired();

            builder.Property(x => x.AdminResolution)
                .HasMaxLength(4000);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.Category)
                .IsRequired();

            builder.Property(x => x.ModerationVerdict)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.TargetRealtorId);
            builder.HasIndex(x => x.DealId);
            builder.HasIndex(x => x.PropertyId);
            builder.HasIndex(x => x.Category);
            builder.HasIndex(x => x.Status);
        }
    }
}
