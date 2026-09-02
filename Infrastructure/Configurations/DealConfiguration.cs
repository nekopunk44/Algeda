using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class DealConfiguration : IEntityTypeConfiguration<Deal>
    {
        public void Configure(EntityTypeBuilder<Deal> builder)
        {
            builder.ToTable("Deals");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.Source)
                .IsRequired();

            builder.Property(x => x.RequestMessage)
                .HasMaxLength(4000);

            builder.Property(x => x.AcceptedAtUtc);

            builder.Property(x => x.RejectedAtUtc);

            builder.Property(x => x.PriorityRealtorId);

            builder.Property(x => x.PriorityUntilUtc);

            builder.Property(x => x.CompletedAt);

            builder.Property(x => x.RealtorCommissionPercent)
                .HasPrecision(5, 2)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.Ignore(x => x.IsIncoming);

            builder.Ignore(x => x.CommissionAmount);
            builder.Ignore(x => x.CommissionCurrency);
            builder.Ignore(x => x.RealtorPayoutAmount);
            builder.Ignore(x => x.RealtorPayoutCurrency);
            builder.Ignore(x => x.AgencyNetCommissionAmount);
            builder.Ignore(x => x.AgencyNetCommissionCurrency);

            builder.OwnsOne(x => x.Commission, money =>
            {
                money.Property(x => x.Amount)
                    .HasColumnName("CommissionAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(x => x.Currency)
                    .HasColumnName("CommissionCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            builder.OwnsOne(x => x.RealtorPayout, money =>
            {
                money.Property(x => x.Amount)
                    .HasColumnName("RealtorPayoutAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(x => x.Currency)
                    .HasColumnName("RealtorPayoutCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            builder.OwnsOne(x => x.AgencyNetCommission, money =>
            {
                money.Property(x => x.Amount)
                    .HasColumnName("AgencyNetCommissionAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(x => x.Currency)
                    .HasColumnName("AgencyNetCommissionCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            builder.HasMany(x => x.Notes)
                .WithOne()
                .HasForeignKey(x => x.DealId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.RealtorId);
            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.PropertyId);
            builder.HasIndex(x => x.ClientRequirementId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.Source);
            builder.HasIndex(x => x.CompletedAt);
            builder.HasIndex(x => x.PriorityRealtorId);
            builder.HasIndex(x => x.PriorityUntilUtc);
            builder.HasIndex(x => new { x.Status, x.RealtorId });
        }
    }
}
