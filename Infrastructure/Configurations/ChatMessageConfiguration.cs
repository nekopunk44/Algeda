using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations
{
    public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
    {
        public void Configure(EntityTypeBuilder<ChatMessage> builder)
        {
            builder.ToTable("ChatMessages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Content)
                .HasMaxLength(4000)
                .IsRequired();

            builder.Property(x => x.IsRead)
                .IsRequired();

            builder.Property(x => x.DealId)
                .IsRequired(false);

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => new { x.SenderId, x.ReceiverId, x.CreatedDate });
            builder.HasIndex(x => new { x.DealId, x.CreatedDate });
        }
    }
}
