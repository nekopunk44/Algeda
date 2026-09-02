using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Web.Services;

namespace Web.Controllers;

[Authorize(Policy = "RequireAnyAuthorized")]
public class NotificationsController : Controller
{
    private readonly ClientDashboardApiClient _clientDashboardApiClient;
    private readonly DealChatApiClient _dealChatApiClient;

    public NotificationsController(
        ClientDashboardApiClient clientDashboardApiClient,
        DealChatApiClient dealChatApiClient)
    {
        _clientDashboardApiClient = clientDashboardApiClient;
        _dealChatApiClient = dealChatApiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Matching");
    }

    [HttpGet]
    public async Task<IActionResult> Widget(CancellationToken cancellationToken = default)
    {
        var notifications = new List<object>();
        var chatNotificationsResult = await _dealChatApiClient.GetUnreadNotificationsAsync(20, cancellationToken);

        if (chatNotificationsResult.Data is not null)
        {
            notifications.AddRange(chatNotificationsResult.Data
                .Where(x => x.DealId != Guid.Empty)
                .Select(x => new
                {
                    id = $"chat:{x.DealId}",
                    source = "chat",
                    title = string.IsNullOrWhiteSpace(x.CounterpartyName)
                        ? "Новое сообщение"
                        : $"Новое сообщение: {x.CounterpartyName}",
                    message = string.IsNullOrWhiteSpace(x.Preview)
                        ? "Откройте чат, чтобы прочитать сообщение."
                        : x.Preview,
                    occurredAtUtc = x.LastMessageAtUtc,
                    unreadCount = Math.Max(0, x.UnreadCount),
                    url = Url.Action("Deal", "DealChat", new { dealId = x.DealId })
                }));
        }

        if (User.IsInRole("Client"))
        {
            var requirementResult = await _clientDashboardApiClient.GetMyActiveRequirementAsync(cancellationToken);
            if (requirementResult.Data is not null)
            {
                var historyResult = await _clientDashboardApiClient.GetRequirementNotificationHistoryAsync(
                    requirementResult.Data.Id,
                    20,
                    cancellationToken);

                if (historyResult.Data is not null)
                {
                    notifications.AddRange(historyResult.Data.Select(item => new
                    {
                        id = $"matching:{item.PropertyId}:{item.SentAtUtc:O}",
                        source = "matching",
                        title = BuildMatchingTitle(item.NotificationType),
                        message = BuildMatchingMessage(item.Title, item.Price, item.Area),
                        occurredAtUtc = item.SentAtUtc,
                        unreadCount = 1,
                        url = Url.Action("Details", "Properties", new { id = item.PropertyId })
                    }));
                }
            }
        }

        return Json(new { notifications });
    }

    [HttpGet]
    public async Task<IActionResult> MarkRead(CancellationToken cancellationToken = default)
    {
        var result = await _dealChatApiClient.MarkNotificationsReadAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return Json(new
            {
                success = false,
                message = result.Error?.Message ?? "Не удалось отметить уведомления как прочитанные."
            });
        }

        return Json(new { success = true });
    }

    private static string BuildMatchingTitle(string notificationType)
    {
        return string.Equals(notificationType, "Новый подходящий объект", StringComparison.OrdinalIgnoreCase)
            ? "Автоподбор: новый подходящий объект"
            : "Автоподбор: подборка отправлена";
    }

    private static string BuildMatchingMessage(string title, decimal price, double area)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Без названия" : title.Trim();
        var numberCulture = CultureInfo.GetCultureInfo("ru-RU");
        var priceText = price.ToString("N0", numberCulture);
        var areaText = area.ToString("N1", numberCulture);

        return $"{safeTitle}. {priceText} USD. {areaText} м²";
    }
}
