using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class DealDocumentConfiguration : IEntityTypeConfiguration<DealDocument>
    {
        public void Configure(EntityTypeBuilder<DealDocument> builder)
        {
            builder.ToTable("DealDocuments");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.OriginalFileName)
                .HasMaxLength(260)
                .IsRequired();

            builder.Property(x => x.ContentType)
                .HasMaxLength(120)
                .IsRequired();

            builder.Property(x => x.FileSizeBytes)
                .IsRequired();

            builder.Property(x => x.ContentHash)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(x => x.Content)
                .IsRequired();

            builder.Property(x => x.UploadedByDisplayName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.UploadedByEmail)
                .HasMaxLength(256);

            builder.Property(x => x.DeletedByDisplayName)
                .HasMaxLength(200);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasOne<Deal>()
                .WithMany()
                .HasForeignKey(x => x.DealId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.DealId);
            builder.HasIndex(x => new { x.DealId, x.IsDeleted, x.CreatedDate });
            builder.HasIndex(x => x.UploadedByUserId);
            builder.HasIndex(x => x.DeletedByUserId);
        }
    }
}
