using Application.DTOs.PropertyPhotos;

namespace Application.Interfaces;

public interface IPropertyPhotoStorageService
{
    Task<IReadOnlyList<PropertyPhotoUploadResult>> UploadForProperties(
        IReadOnlyList<PropertyPhotoUploadItem> items,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PropertyPhotoUploadResult>> UploadForRealtors(
        IReadOnlyList<PropertyPhotoUploadItem> items,
        CancellationToken cancellationToken);

    Task<PropertyPhotoReadResult?> GetByPath(
        string path,
        CancellationToken cancellationToken);

    Task<PropertyPhotoMigrationResult> MigrateLegacyPropertyPhotos(
        string webRootPath,
        bool deleteSourceFilesAfterImport,
        CancellationToken cancellationToken);
}
