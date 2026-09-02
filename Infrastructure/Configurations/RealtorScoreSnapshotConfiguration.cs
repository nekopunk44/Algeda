using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class RealtorScoreSnapshotConfiguration : IEntityTypeConfiguration<RealtorScoreSnapshot>
    {
        public void Configure(EntityTypeBuilder<RealtorScoreSnapshot> builder)
        {
            builder.ToTable("RealtorScoreSnapshots");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.Property(x => x.ClientTrustScore)
                .IsRequired();

            builder.Property(x => x.AdminPerformanceScore)
                .IsRequired();

            builder.Property(x => x.ClientServiceScoreComponent).IsRequired();
            builder.Property(x => x.PropertyAccuracyScoreComponent).IsRequired();
            builder.Property(x => x.ComplaintPenaltyComponent).IsRequired();
            builder.Property(x => x.PropertyDataQualityComponent).IsRequired();
            builder.Property(x => x.WorkflowDisciplineComponent).IsRequired();
            builder.Property(x => x.BusinessResultComponent).IsRequired();
            builder.Property(x => x.ReputationRiskComponent).IsRequired();

            builder.Property(x => x.CalculationVersion)
                .HasMaxLength(64);

            builder.HasIndex(x => x.RealtorId);
            builder.HasIndex(x => new { x.RealtorId, x.CreatedDate });
        }
    }
}
