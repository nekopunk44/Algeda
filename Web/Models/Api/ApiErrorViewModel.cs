namespace Web.Models.Api
{
    public sealed class ApiErrorViewModel
    {
        public int StatusCode { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;
    }
}
