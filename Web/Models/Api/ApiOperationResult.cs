namespace Web.Models.Api;

public sealed class ApiOperationResult
{
    public ApiErrorViewModel? Error { get; }

    public string? Message { get; }

    public bool IsSuccess => Error is null;

    private ApiOperationResult(ApiErrorViewModel? error, string? message = null)
    {
        Error = error;
        Message = message;
    }

    public static ApiOperationResult Success(string? message = null) => new(null, message);

    public static ApiOperationResult Failure(ApiErrorViewModel error) => new(error);
}
