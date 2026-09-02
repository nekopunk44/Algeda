using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class CurrencyRateConfiguration : IEntityTypeConfiguration<CurrencyRate>
    {
        public void Configure(EntityTypeBuilder<CurrencyRate> builder)
        {
            builder.ToTable("CurrencyRates");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.RateToBase)
                .HasPrecision(18, 6)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.HasIndex(x => x.IsActive);
        }
    }
}
