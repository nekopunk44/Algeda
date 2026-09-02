using Web.Models.Api;
using Web.Models.Dashboard;

namespace Web.Models.Notifications;

public sealed class NotificationsIndexViewModel
{
    public DashboardHealthViewModel? AutoMatchingHealth { get; init; }

    public ApiErrorViewModel? AutoMatchingError { get; init; }

    public Guid? PropertyId { get; init; }

    public int RequirementsLimit { get; init; } = 500;

    public int? SubscribedClientsCount { get; init; }

    public ApiErrorViewModel? SubscribersError { get; init; }

    public bool CanInspectSubscribers { get; init; }
}
