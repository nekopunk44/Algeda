using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class PropertyCriterionValueConfiguration : IEntityTypeConfiguration<PropertyCriterionValue>
    {
        public void Configure(EntityTypeBuilder<PropertyCriterionValue> builder)
        {
            builder.ToTable("PropertyCriterionValues");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.PropertyId)
                .IsRequired();

            builder.Property(x => x.CriterionDefinitionId)
                .IsRequired();

            builder.Property(x => x.Value)
                .HasMaxLength(4000)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasOne(x => x.Property)
                .WithMany(x => x.CriterionValues)
                .HasForeignKey(x => x.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.CriterionDefinition)
                .WithMany()
                .HasForeignKey(x => x.CriterionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.PropertyId, x.CriterionDefinitionId })
                .IsUnique();

            builder.HasIndex(x => x.CriterionDefinitionId);
        }
    }
}
