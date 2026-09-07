using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using DomainLocation = Domain.ValueObjects.Location;

namespace Infrastructure.Configurations
{
    public class ClientRequirementConfiguration : IEntityTypeConfiguration<ClientRequirement>
    {
        public void Configure(EntityTypeBuilder<ClientRequirement> builder)
        {
            builder.ToTable("ClientRequirements");

            builder.HasKey(x => x.Id);

            var locationConverter = new ValueConverter<DomainLocation, Point>(
                location => new Point(location.Longitude, location.Latitude) { SRID = 4326 },
                point => new DomainLocation(point.Y, point.X));

            var desiredTypesConverter = new ValueConverter<List<PropertyType>, int[]>(
                types => (types ?? Enumerable.Empty<PropertyType>()).Select(x => (int)x).ToArray(),
                values => (values ?? Array.Empty<int>()).Select(x => (PropertyType)x).ToList());

            var desiredTypesComparer = new ValueComparer<List<PropertyType>>(
                (left, right) => (left ?? Enumerable.Empty<PropertyType>()).SequenceEqual(right ?? Enumerable.Empty<PropertyType>()),
                value => (value ?? Enumerable.Empty<PropertyType>()).Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                value => (value ?? Enumerable.Empty<PropertyType>()).ToList());

            builder.Property(x => x.TargetLocation)
                .HasConversion(locationConverter)
                .HasColumnType("geography (point)")
                .IsRequired();

            builder.Property(x => x.DesiredTypes)
                .HasConversion(desiredTypesConverter)
                .Metadata.SetValueComparer(desiredTypesComparer);
            builder.Property(x => x.DesiredTypes)
                .HasColumnType("integer[]")
                .IsRequired();

            builder.Property(x => x.SearchRadiusMeters)
                .IsRequired();

            builder.Property(x => x.IgnoreArea)
                .IsRequired();

            builder.Property(x => x.MinPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.MaxPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.MinArea)
                .IsRequired();

            builder.Property(x => x.MaxArea)
                .IsRequired(false);

            builder.Property(x => x.AddressQuery)
                .HasMaxLength(500)
                .IsRequired(false);

            builder.Property(x => x.MinMatchPercentage)
                .IsRequired();

            builder.Property(x => x.PriceWeight)
                .IsRequired();

            builder.Property(x => x.AreaWeight)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.TargetLocation)
                .HasMethod("GIST");
        }
    }
}
