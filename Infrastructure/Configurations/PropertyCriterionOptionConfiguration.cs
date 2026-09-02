using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class PropertyCriterionOptionConfiguration : IEntityTypeConfiguration<PropertyCriterionOption>
    {
        public void Configure(EntityTypeBuilder<PropertyCriterionOption> builder)
        {
            builder.ToTable("PropertyCriterionOptions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Value)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Label)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.SortOrder)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => new { x.PropertyCriterionDefinitionId, x.Value })
                .IsUnique();

            builder.HasIndex(x => new { x.PropertyCriterionDefinitionId, x.SortOrder });
        }
    }
}
