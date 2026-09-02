using Web.Models.Api;

namespace Web.Models.Realtors;

public sealed class RealtorsIndexViewModel
{
    public IReadOnlyList<RealtorCardViewModel> TopRealtors { get; init; } = [];

    public IReadOnlyList<RealtorCardViewModel> Realtors { get; init; } = [];

    public string? SearchQuery { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 9;

    public int TotalCount { get; init; }

    public int TotalPages { get; init; } = 1;

    public ApiErrorViewModel? ApiError { get; init; }
}
