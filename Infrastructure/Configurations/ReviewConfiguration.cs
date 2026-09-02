using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ReviewConfiguration : IEntityTypeConfiguration<Review>
    {
        public void Configure(EntityTypeBuilder<Review> builder)
        {
            builder.ToTable("Reviews");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Comment)
                .HasMaxLength(4000);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.Property(x => x.Score)
                .HasColumnName("Score")
                .IsRequired();

            builder.Ignore(x => x.Rating);

            builder.HasIndex(x => x.DealId)
                .IsUnique();
            builder.HasIndex(x => x.RealtorId);
            builder.HasIndex(x => x.ClientId);
        }
    }
}
