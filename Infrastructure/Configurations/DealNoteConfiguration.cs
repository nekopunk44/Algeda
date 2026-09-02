using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class DealNoteConfiguration : IEntityTypeConfiguration<DealNote>
    {
        public void Configure(EntityTypeBuilder<DealNote> builder)
        {
            builder.ToTable("DealNotes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Text)
                .HasMaxLength(4000)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.DealId);
            builder.HasIndex(x => x.AuthorRealtorId);
            builder.HasIndex(x => x.CreatedDate);
        }
    }
}
