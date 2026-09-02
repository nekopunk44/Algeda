using System.Security.Cryptography;
using Application.DTOs.PropertyPhotos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Services;

public sealed class PropertyPhotoStorageService : IPropertyPhotoStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly AppDbContext _dbContext;
    private readonly PropertyPhotoStorageOptions _options;
    private readonly ILogger<PropertyPhotoStorageService> _logger;

    public PropertyPhotoStorageService(
        AppDbContext dbContext,
        IOptions<PropertyPhotoStorageOptions> options,
        ILogger<PropertyPhotoStorageService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PropertyPhotoUploadResult>> UploadForProperties(
        IReadOnlyList<PropertyPhotoUploadItem> items,
        CancellationToken cancellationToken)
    {
        return await UploadInternal(items, "/uploads/properties", _options.MaxFilesPerRequest, cancellationToken);
    }

    public async Task<IReadOnlyList<PropertyPhotoUploadResult>> UploadForRealtors(
        IReadOnlyList<PropertyPhotoUploadItem> items,
        CancellationToken cancellationToken)
    {
        return await UploadInternal(items, "/uploads/realtors", 1, cancellationToken);
    }

    private async Task<IReadOnlyList<PropertyPhotoUploadResult>> UploadInternal(
        IReadOnlyList<PropertyPhotoUploadItem> items,
        string rootPath,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        if (items.Count > maxFiles)
        {
            throw new ValidationException($"Можно загрузить не более {maxFiles} фотографий за один запрос.");
        }

        var now = DateTime.UtcNow;
        var datePart = now.ToString("yyyyMMdd");
        var created = new List<PropertyPhotoBlob>(items.Count);
        var result = new List<PropertyPhotoUploadResult>(items.Count);

        foreach (var item in items)
        {
            if (item.Content.Length == 0 || item.Content.Length > _options.MaxUploadBytes)
            {
                throw new ValidationException("Размер фотографии превышает допустимый предел.");
            }

            var sourceExtension = NormalizeExtension(item.FileName);
            if (!AllowedExtensions.Contains(sourceExtension))
            {
                throw new ValidationException("Допустимы только изображения JPG, PNG и WEBP.");
            }

            var prepared = await PrepareImage(item.Content, sourceExtension, cancellationToken);
            var fileName = $"{Guid.NewGuid():N}{prepared.Extension}";
            var path = $"{rootPath}/{datePart}/{fileName}";
            var hash = ComputeSha256(prepared.Content);

            created.Add(new PropertyPhotoBlob(path, prepared.ContentType, prepared.Content, hash));
            result.Add(new PropertyPhotoUploadResult(path, prepared.ContentType, prepared.Content.Length));
        }

        await _dbContext.PropertyPhotoBlobs.AddRangeAsync(created, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<PropertyPhotoReadResult?> GetByPath(
        string path,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        var item = await _dbContext.PropertyPhotoBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Path == normalizedPath, cancellationToken);

        if (item is null)
        {
            return null;
        }

        return new PropertyPhotoReadResult(item.Path, item.ContentType, item.Content);
    }

    public async Task<PropertyPhotoMigrationResult> MigrateLegacyPropertyPhotos(
        string webRootPath,
        bool deleteSourceFilesAfterImport,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            throw new ValidationException("Путь к web root не задан для миграции фото.");
        }

        var normalizedRoot = Path.GetFullPath(webRootPath);
        if (!Directory.Exists(normalizedRoot))
        {
            _logger.LogWarning("Legacy migration skipped: root path not found: {Path}", normalizedRoot);
            return new PropertyPhotoMigrationResult(0, 0, 0, 0);
        }

        var properties = await _dbContext.Properties
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var candidatePaths = properties
            .SelectMany(x => x.PhotoPaths ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizePath)
            .Where(x => x.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidatePaths.Count == 0)
        {
            return new PropertyPhotoMigrationResult(0, 0, 0, 0);
        }

        var existing = await _dbContext.PropertyPhotoBlobs
            .AsNoTracking()
            .Where(x => candidatePaths.Contains(x.Path))
            .Select(x => x.Path)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;
        var missing = 0;
        var failed = 0;

        foreach (var photoPath in candidatePaths)
        {
            if (existingSet.Contains(photoPath))
            {
                skipped++;
                continue;
            }

            var sourceFilePath = BuildLegacyAbsolutePath(normalizedRoot, photoPath);
            if (!File.Exists(sourceFilePath))
            {
                missing++;
                continue;
            }

            try
            {
                var content = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
                if (content.Length == 0 || content.Length > _options.MaxUploadBytes)
                {
                    failed++;
                    continue;
                }

                var extension = NormalizeExtension(sourceFilePath);
                var prepared = await PrepareImage(content, extension, cancellationToken);
                var hash = ComputeSha256(prepared.Content);

                await _dbContext.PropertyPhotoBlobs.AddAsync(
                    new PropertyPhotoBlob(photoPath, prepared.ContentType, prepared.Content, hash),
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                imported++;
                if (deleteSourceFilesAfterImport)
                {
                    File.Delete(sourceFilePath);
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex, "Failed to migrate legacy property photo: {Path}", photoPath);
            }
        }

        return new PropertyPhotoMigrationResult(imported, skipped, missing, failed);
    }

    private async Task<PreparedImage> PrepareImage(
        byte[] content,
        string sourceExtension,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = new MemoryStream(content);
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        if (image.Width < 64 || image.Height < 64)
        {
            throw new ValidationException("Изображение слишком маленькое.");
        }

        image.Mutate(x =>
        {
            x.AutoOrient();
            x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(_options.MaxWidth, _options.MaxHeight)
            });
        });

        await using var output = new MemoryStream();
        switch (sourceExtension.ToLowerInvariant())
        {
            case ".png":
                await image.SaveAsPngAsync(output, new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.Level6
                }, cancellationToken);
                return new PreparedImage(output.ToArray(), ".png", "image/png");
            case ".webp":
                await image.SaveAsWebpAsync(output, new WebpEncoder
                {
                    Quality = _options.JpegQuality,
                    FileFormat = WebpFileFormatType.Lossy
                }, cancellationToken);
                return new PreparedImage(output.ToArray(), ".webp", "image/webp");
            default:
                await image.SaveAsJpegAsync(output, new JpegEncoder
                {
                    Quality = _options.JpegQuality
                }, cancellationToken);
                return new PreparedImage(output.ToArray(), ".jpg", "image/jpeg");
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ValidationException("Путь к фото не может быть пустым.");
        }

        var normalized = path.Trim().Replace("\\", "/");
        return normalized.StartsWith("/", StringComparison.Ordinal)
            ? normalized
            : "/" + normalized;
    }

    private static string NormalizeExtension(string fileNameOrPath)
    {
        var extension = Path.GetExtension(fileNameOrPath)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ValidationException("Расширение файла не определено.");
        }

        return extension;
    }

    private static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildLegacyAbsolutePath(string webRootPath, string photoPath)
    {
        var trimmed = photoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(webRootPath, trimmed);
    }

    private sealed record PreparedImage(byte[] Content, string Extension, string ContentType);
}
