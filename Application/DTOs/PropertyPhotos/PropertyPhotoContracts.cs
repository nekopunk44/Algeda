namespace Application.DTOs.PropertyPhotos;

public sealed record PropertyPhotoUploadItem(
    string FileName,
    string? ContentType,
    byte[] Content);

public sealed record PropertyPhotoUploadResult(
    string Path,
    string ContentType,
    int ContentLength);

public sealed record PropertyPhotoReadResult(
    string Path,
    string ContentType,
    byte[] Content);

public sealed record PropertyPhotoMigrationResult(
    int ImportedCount,
    int SkippedExistingCount,
    int MissingSourceCount,
    int FailedCount);
