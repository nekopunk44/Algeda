using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class PropertyCriterionDefinitionConfiguration : IEntityTypeConfiguration<PropertyCriterionDefinition>
    {
        public void Configure(EntityTypeBuilder<PropertyCriterionDefinition> builder)
        {
            builder.ToTable("PropertyCriterionDefinitions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Category)
                .HasMaxLength(150);

            builder.Property(x => x.Description)
                .HasMaxLength(2000);

            builder.Property(x => x.ValueType)
                .IsRequired();

            builder.Property(x => x.IsHidden)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasMany(x => x.Options)
                .WithOne()
                .HasForeignKey(x => x.PropertyCriterionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.HasIndex(x => x.IsHidden);
            builder.HasIndex(x => x.Category);
        }
    }
}
