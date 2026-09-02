using API.Auth;
using Application.DTOs.PropertyPhotos;
using Application.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/property-photos")]
public sealed class PropertyPhotosController : ApiControllerBase
{
    private readonly IPropertyPhotoStorageService _photoStorageService;

    public PropertyPhotosController(IPropertyPhotoStorageService photoStorageService)
    {
        _photoStorageService = photoStorageService;
    }

    [HttpPost("upload")]
    [Authorize(Policy = AuthorizationPolicies.ClientOrRealtorOrAdmin)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60 * 1024 * 1024)]
    public Task<IActionResult> Upload(
        [FromForm(Name = "files")] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var items = (files ?? [])
                .Where(x => x is not null && x.Length > 0)
                .Select(async file =>
                {
                    await using var stream = file.OpenReadStream();
                    await using var copy = new MemoryStream();
                    await stream.CopyToAsync(copy, cancellationToken);
                    return new PropertyPhotoUploadItem(
                        file.FileName,
                        file.ContentType,
                        copy.ToArray());
                })
                .ToList();

            var payload = await Task.WhenAll(items);
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

            return File(photo.Content, photo.ContentType);
        });
    }

    [HttpPost("migrate-legacy")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public Task<IActionResult> MigrateLegacy(
        [FromQuery] string webRootPath,
        [FromQuery] bool deleteSourceFilesAfterImport = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async () =>
        {
            var result = await _photoStorageService.MigrateLegacyPropertyPhotos(
                webRootPath,
                deleteSourceFilesAfterImport,
                cancellationToken);
            return Ok(result);
        });
    }
}
