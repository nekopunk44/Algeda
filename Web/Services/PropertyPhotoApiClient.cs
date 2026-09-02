using Web.Models.Api;

namespace Web.Services;

public sealed class PropertyPhotoApiClient
{
    private readonly HttpClient _httpClient;

    public PropertyPhotoApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiClientResult<IReadOnlyList<string>>> UploadPhotosAsync(
        IEnumerable<IFormFile>? files,
        CancellationToken cancellationToken = default)
    {
        var preparedFiles = (files ?? [])
            .Where(x => x is not null && x.Length > 0)
            .ToList();

        if (preparedFiles.Count == 0)
        {
            return ApiClientResult<IReadOnlyList<string>>.Success([]);
        }

        try
        {
            using var form = new MultipartFormDataContent();
            foreach (var file in preparedFiles)
            {
                var stream = file.OpenReadStream();
                var content = new StreamContent(stream);
                if (!string.IsNullOrWhiteSpace(file.ContentType))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                }

                form.Add(content, "files", file.FileName);
            }

            using var response = await _httpClient.PostAsync("api/property-photos/upload", form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiClientResult<IReadOnlyList<string>>.Failure(
                    await ApiClientSupport.BuildErrorAsync(response, cancellationToken));
            }

            var payload = await response.Content.ReadFromJsonAsync<List<PhotoUploadResponseDto>>(
                ApiClientSupport.JsonOptions,
                cancellationToken);

            if (payload is null)
            {
                return ApiClientResult<IReadOnlyList<string>>.Failure(
                    ApiClientSupport.BuildEmptyPayloadError("API returned an empty property photo upload payload."));
            }

            return ApiClientResult<IReadOnlyList<string>>.Success(payload.Select(x => x.Path).ToList());
        }
        catch (HttpRequestException)
        {
            return ApiClientResult<IReadOnlyList<string>>.Failure(ApiClientSupport.BuildUnavailableApiError());
        }
        catch (TaskCanceledException)
        {
            return ApiClientResult<IReadOnlyList<string>>.Failure(ApiClientSupport.BuildTimeoutApiError());
        }
    }

    public async Task<PropertyPhotoContentResult?> GetByPathAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var query = $"api/property-photos/by-path?path={Uri.EscapeDataString(path)}";
        try
        {
            using var response = await _httpClient.GetAsync(query, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length == 0)
            {
                return null;
            }

            return new PropertyPhotoContentResult(contentType, content);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private sealed class PhotoUploadResponseDto
    {
        public string Path { get; set; } = string.Empty;
    }
}

public sealed record PropertyPhotoContentResult(string ContentType, byte[] Content);
