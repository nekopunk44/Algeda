using API.Auth;
using Application.DTOs.PropertyPhotos;
using Application.Exceptions;
using Application.Interfaces;
using Application.Options;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API.Controllers;

[ApiController]
[Route("api/property-photos")]
public sealed class PropertyPhotosController : ApiControllerBase
{
    private readonly IPropertyPhotoStorageService _photoStorageService;
    private readonly PropertyPhotoStorageOptions _options;

    public PropertyPhotosController(
        IPropertyPhotoStorageService photoStorageService,
        IOptions<PropertyPhotoStorageOptions> options)
    {
        _photoStorageService = photoStorageService;
        _options = options.Value;
    }

    [HttpPost("upload")]
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024)]
    public Task<IActionResult> Upload(
        [FromForm(Name = "files")] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var uploadedFiles = (files ?? []).Where(file => file is not null).ToList();
            ValidateUploadMetadata(uploadedFiles);

            var payload = new List<PropertyPhotoUploadItem>(uploadedFiles.Count);
            foreach (var file in uploadedFiles)
            {
                await using var stream = file.OpenReadStream();
                await using var copy = new MemoryStream((int)file.Length);
                await stream.CopyToAsync(copy, cancellationToken);

                if (copy.Length == 0 || copy.Length > _options.MaxUploadBytes)
                {
                    throw new ValidationException("Размер фотографии превышает допустимый предел.");
                }

                payload.Add(new PropertyPhotoUploadItem(
                    file.FileName,
                    file.ContentType,
                    copy.ToArray()));
            }

            var result = await _photoStorageService.UploadForProperties(payload, cancellationToken);
            return Ok(result);
        });
    }

    [HttpGet("by-path")]
    [AllowAnonymous]
    public Task<IActionResult> GetByPath([FromQuery] string path, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var photo = await _photoStorageService.GetByPath(path, cancellationToken);
            if (photo is null)
            {
                return NotFound();
            }

            // Блобы фотографий неизменяемы и адресуются путём, поэтому глобальный
            // no-store здесь заменяется долгим публичным кешированием.
            Response.Headers.CacheControl = "public, max-age=86400, immutable";
            return File(photo.Content, photo.ContentType);
        });
    }

    private void ValidateUploadMetadata(IReadOnlyCollection<IFormFile> files)
    {
        if (files.Count == 0)
        {
            throw new ValidationException("Выберите хотя бы одну фотографию.");
        }

        if (files.Count > _options.MaxFilesPerRequest)
        {
            throw new ValidationException(
                $"Можно загрузить не более {_options.MaxFilesPerRequest} фотографий за один запрос.");
        }

        long totalBytes = 0;
        foreach (var file in files)
        {
            if (file.Length <= 0 || file.Length > _options.MaxUploadBytes)
            {
                throw new ValidationException("Размер фотографии превышает допустимый предел.");
            }

            totalBytes = checked(totalBytes + file.Length);
            if (totalBytes > _options.MaxTotalUploadBytes)
            {
                throw new ValidationException("Суммарный размер фотографий превышает допустимый предел.");
            }
        }
    }
}
