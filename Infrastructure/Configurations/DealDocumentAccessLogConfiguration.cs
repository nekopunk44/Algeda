using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class DealDocumentAccessLogConfiguration : IEntityTypeConfiguration<DealDocumentAccessLog>
    {
        public void Configure(EntityTypeBuilder<DealDocumentAccessLog> builder)
        {
            builder.ToTable("DealDocumentAccessLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DocumentTitle)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.DocumentFileName)
                .HasMaxLength(260)
                .IsRequired();

            builder.Property(x => x.Action)
                .IsRequired();

            builder.Property(x => x.ActorDisplayName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.ActorEmail)
                .HasMaxLength(256);

            builder.Property(x => x.ActorRole)
                .HasMaxLength(80)
                .IsRequired();

            builder.Property(x => x.IpAddress)
                .HasMaxLength(80);

            builder.Property(x => x.UserAgent)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasOne<Deal>()
                .WithMany()
                .HasForeignKey(x => x.DealId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<DealDocument>()
                .WithMany()
                .HasForeignKey(x => x.DealDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.DealId);
            builder.HasIndex(x => x.DealDocumentId);
            builder.HasIndex(x => x.Action);
            builder.HasIndex(x => x.ActorUserId);
            builder.HasIndex(x => x.CreatedDate);
            builder.HasIndex(x => new { x.Action, x.CreatedDate });
        }
    }
}
