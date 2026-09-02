using System.ComponentModel.DataAnnotations;

namespace Application.Options;

public sealed class PropertyPhotoStorageOptions
{
    public const string SectionName = "PropertyPhotoStorage";

    [Range(1, 50 * 1024 * 1024)]
    public int MaxUploadBytes { get; set; } = 12 * 1024 * 1024;

    [Range(256, 8192)]
    public int MaxWidth { get; set; } = 2560;

    [Range(256, 8192)]
    public int MaxHeight { get; set; } = 2560;

    [Range(50, 100)]
    public int JpegQuality { get; set; } = 85;

    [Range(1, 20)]
    public int MaxFilesPerRequest { get; set; } = 20;

    public LegacyPropertyPhotoMigrationOptions LegacyMigration { get; set; } = new();
}

public sealed class LegacyPropertyPhotoMigrationOptions
{
    public bool Enabled { get; set; } = true;

    public string WebRootPath { get; set; } = @"..\Web\wwwroot";

    public bool DeleteSourceFilesAfterImport { get; set; } = false;
}
