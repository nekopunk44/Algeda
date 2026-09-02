using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class RealtorRegistrationRequestConfiguration : IEntityTypeConfiguration<RealtorRegistrationRequest>
    {
        public void Configure(EntityTypeBuilder<RealtorRegistrationRequest> builder)
        {
            builder.ToTable("RealtorRegistrationRequests");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.MiddleName)
                .HasMaxLength(100);

            builder.Property(x => x.Email)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(x => x.PhoneNumber)
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.ReviewComment)
                .HasMaxLength(1000);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.Email);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => new { x.Email, x.Status });
        }
    }
}
