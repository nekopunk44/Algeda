using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ClientRequirementCriterionConfiguration : IEntityTypeConfiguration<ClientRequirementCriterion>
    {
        public void Configure(EntityTypeBuilder<ClientRequirementCriterion> builder)
        {
            builder.ToTable("ClientRequirementCriteria");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClientRequirementId)
                .IsRequired();

            builder.Property(x => x.CriterionDefinitionId)
                .IsRequired();

            builder.Property(x => x.Priority)
                .IsRequired();

            builder.Property(x => x.Value)
                .HasColumnType("text");

            builder.Property(x => x.ValuesJson)
                .HasColumnType("text");

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasOne(x => x.ClientRequirement)
                .WithMany(x => x.Criteria)
                .HasForeignKey(x => x.ClientRequirementId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PropertyCriterionDefinition>()
                .WithMany()
                .HasForeignKey(x => x.CriterionDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.ClientRequirementId, x.CriterionDefinitionId })
                .IsUnique();

            builder.HasIndex(x => x.CriterionDefinitionId);
            builder.HasIndex(x => x.Priority);
        }
    }
}
