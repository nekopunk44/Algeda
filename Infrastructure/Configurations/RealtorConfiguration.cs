using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Infrastructure.Configurations
{
    public class RealtorConfiguration : IEntityTypeConfiguration<Realtor>
    {
        private sealed record FullNameStorage(string FirstName, string LastName, string? MiddleName);

        public void Configure(EntityTypeBuilder<Realtor> builder)
        {
            builder.ToTable("Realtors");

            builder.HasKey(x => x.Id);

            var fullNameConverter = new ValueConverter<FullName, string>(
                fullName => SerializeFullName(fullName),
                json => DeserializeFullName(json));

            builder.Property(x => x.PhoneNumber)
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.AvatarPath)
                .HasMaxLength(500);

            builder.Property(x => x.FullName)
                .HasConversion(fullNameConverter)
                .HasColumnName("FullName")
                .HasColumnType("text")
                .IsRequired();

            builder.Property(x => x.CurrentKpiScore)
                .IsRequired();

            builder.Property(x => x.AverageRating)
                .IsRequired();

            builder.Property(x => x.DealsThisMonth)
                .IsRequired();

            builder.Property(x => x.Level)
                .IsRequired();

            builder.Property(x => x.IsLevelManuallyAssigned)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.PhoneNumber)
                .IsUnique();
            builder.HasIndex(x => x.Level);
            builder.HasIndex(x => x.CurrentKpiScore);
        }

        private static string SerializeFullName(FullName fullName)
        {
            return JsonSerializer.Serialize(new FullNameStorage(
                fullName.FirstName,
                fullName.LastName,
                fullName.MiddleName));
        }

        private static FullName DeserializeFullName(string json)
        {
            var data = JsonSerializer.Deserialize<FullNameStorage>(json)
                ?? new FullNameStorage("Unknown", "Unknown", null);

            return new FullName(data.FirstName, data.LastName, data.MiddleName);
        }
    }
}
