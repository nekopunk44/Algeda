using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class PropertyPhotoBlobConfiguration : IEntityTypeConfiguration<PropertyPhotoBlob>
{
    public void Configure(EntityTypeBuilder<PropertyPhotoBlob> builder)
    {
        builder.ToTable("PropertyPhotoBlobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Path)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ContentLength)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasIndex(x => x.Path)
            .IsUnique();
    }
}
