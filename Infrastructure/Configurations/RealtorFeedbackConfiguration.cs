using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class RealtorFeedbackConfiguration : IEntityTypeConfiguration<RealtorFeedback>
    {
        public void Configure(EntityTypeBuilder<RealtorFeedback> builder)
        {
            builder.ToTable("RealtorFeedbacks");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Comment)
                .HasMaxLength(4000);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.Property(x => x.ServiceScore)
                .HasColumnName("ServiceScore")
                .IsRequired();
            builder.Ignore(x => x.ServiceRating);

            builder.Property(x => x.FormType)
                .IsRequired();

            builder.Property(x => x.CommunicationScore);

            builder.Property(x => x.ResponsivenessScore);

            builder.Property(x => x.ExpertiseScore);

            builder.Property(x => x.TitleAccuracyScore);

            builder.Property(x => x.CriteriaAccuracyScore);

            builder.Property(x => x.DescriptionAccuracyScore);

            builder.Property(x => x.PhotosAccuracyScore);

            builder.Property(x => x.TitleAlignment)
                .IsRequired();

            builder.Property(x => x.CriteriaAlignment)
                .IsRequired();

            builder.Property(x => x.DescriptionAlignment)
                .IsRequired();

            builder.Property(x => x.PhotosAlignment)
                .IsRequired();

            builder.HasIndex(x => x.DealId)
                .IsUnique();
            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.ServiceRealtorId);
            builder.HasIndex(x => x.PropertyResponsibleRealtorId);
        }
    }
}
