using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using DomainLocation = Domain.ValueObjects.Location;

namespace Infrastructure.Configurations
{
    public class PropertyConfiguration : IEntityTypeConfiguration<Property>
    {
        public void Configure(EntityTypeBuilder<Property> builder)
        {
            builder.ToTable("Properties");

            builder.HasKey(x => x.Id);

            var locationConverter = new ValueConverter<DomainLocation, Point>(
                location => new Point(location.Longitude, location.Latitude) { SRID = 4326 },
                point => new DomainLocation(point.Y, point.X));

            builder.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            builder.Property(x => x.Address)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Ignore(x => x.OriginalPriceAmount);
            builder.Ignore(x => x.OriginalPriceCurrency);

            builder.OwnsOne(x => x.OriginalPrice, money =>
            {
                money.Property(x => x.Amount)
                    .HasColumnName("OriginalPriceAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(x => x.Currency)
                    .HasColumnName("OriginalPriceCurrency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            builder.Property(x => x.Area)
                .IsRequired();

            builder.Property(x => x.RoomsCount)
                .IsRequired();

            builder.Property(x => x.Type)
                .IsRequired();

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.SoldAtUtc);

            builder.Property(x => x.OwnerFullName)
                .HasMaxLength(300);

            builder.Property(x => x.OwnerEmail)
                .HasMaxLength(256);

            builder.Property(x => x.OwnerPhoneNumber)
                .HasMaxLength(32);

            builder.Property(x => x.OwnerClientId);

            builder.Property(x => x.ResponsibleRealtorId);

            builder.Property(x => x.Location)
                .HasConversion(locationConverter)
                .HasColumnType("geography (point)")
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            var photoPathsComparer = new ValueComparer<List<string>>(
                (left, right) => ArePhotoPathsEqual(left, right),
                value => GetPhotoPathsHash(value),
                value => ClonePhotoPaths(value));

            builder.Property<List<string>>("_photoPaths")
                .HasColumnName("PhotoPaths")
                .HasColumnType("text[]")
                .Metadata.SetValueComparer(photoPathsComparer);

            builder.Ignore(x => x.MainPhotoPath);

            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.Type);
            builder.HasIndex(x => x.Price);
            builder.HasIndex(x => x.ResponsibleRealtorId);
            builder.HasIndex(x => x.OwnerClientId);
            builder.HasIndex(x => new { x.Status, x.Type, x.Price });
            builder.HasIndex(x => x.Location)
                .HasMethod("GIST");
        }

        private static bool ArePhotoPathsEqual(List<string>? left, List<string>? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);
        }

        private static int GetPhotoPathsHash(List<string>? value)
        {
            if (value is null || value.Count == 0)
                return 0;

            var hash = 0;
            foreach (var item in value)
            {
                hash = HashCode.Combine(hash, item.ToLowerInvariant().GetHashCode());
            }

            return hash;
        }

        private static List<string> ClonePhotoPaths(List<string>? value)
        {
            return value is null ? [] : value.ToList();
        }
    }
}
