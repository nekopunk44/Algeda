using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Services;

namespace Web.Controllers;

[AllowAnonymous]
public sealed class UploadsController : Controller
{
    private readonly PropertyPhotoApiClient _photoApiClient;

    public UploadsController(PropertyPhotoApiClient photoApiClient)
    {
        _photoApiClient = photoApiClient;
    }

    [HttpGet("uploads/{**path}")]
    public async Task<IActionResult> PropertyPhoto(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return NotFound();
        }

        var normalizedPath = "/uploads/" + path.TrimStart('/');
        var photo = await _photoApiClient.GetByPathAsync(normalizedPath, cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        return File(photo.Content, photo.ContentType);
    }
}
