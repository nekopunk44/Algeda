using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
    {
        public void Configure(EntityTypeBuilder<PendingRegistration> builder)
        {
            builder.ToTable("PendingRegistrations");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type).IsRequired();

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

            builder.Property(x => x.PasswordHash)
                .HasMaxLength(512)
                .IsRequired();

            builder.Property(x => x.CodeHash)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(x => x.CodeExpiresAtUtc).IsRequired();
            builder.Property(x => x.LastCodeSentAtUtc).IsRequired();
            builder.Property(x => x.CreatedDate).IsRequired();

            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.PhoneNumber);
            builder.HasIndex(x => x.CodeExpiresAtUtc);
        }
    }
}
