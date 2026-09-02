namespace Web.Models.Api;

public sealed record ApiClientResult<T>(T? Data, ApiErrorViewModel? Error)
{
    public bool IsSuccess => Data is not null && Error is null;

    public static ApiClientResult<T> Success(T data) => new(data, null);

    public static ApiClientResult<T> Failure(ApiErrorViewModel error) => new(default, error);
}
