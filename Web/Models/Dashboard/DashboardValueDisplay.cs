namespace Web.Models.Dashboard;

public static class DashboardValueDisplay
{
    public static string DealStatus(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Created" => "Создана",
            "InProgress" => "В работе",
            "Completed" => "Завершена",
            "Cancelled" => "Отменена",
            _ => "Не указано"
        };
    }

    public static string ActivityType(string? rawType)
    {
        return rawType?.Trim() switch
        {
            "PropertyShowing" => "Показ объекта",
            "ClientCall" => "Звонок клиенту",
            "StatusUpdate" => "Обновление статуса",
            "PropertyListing" => "Публикация объекта",
            "DealClosure" => "Закрытие сделки",
            "DealCancellation" => "Отмена сделки",
            _ => "Не указано"
        };
    }

    public static string ComplaintStatus(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Opened" => "Открыта",
            "InProgress" => "В работе",
            "Resolved" => "Решена",
            _ => "Не указано"
        };
    }

    public static string ComplaintCategory(string? rawCategory)
    {
        return rawCategory?.Trim() switch
        {
            "Realtor" => "На риелтора",
            "PropertyDescriptionMismatch" => "Несоответствие описанию недвижимости",
            "PoorPropertyMatching" => "Плохие результаты подбора недвижимости",
            "Other" => "Другое",
            _ => "Не указано"
        };
    }

    public static string ComplaintVerdict(string? rawVerdict)
    {
        return rawVerdict?.Trim() switch
        {
            "Confirmed" => "Подтверждена",
            "PartiallyConfirmed" => "Частично подтверждена",
            "NotConfirmed" => "Не подтверждена",
            _ => "Не выбран"
        };
    }

    public static string CriterionValueType(string? rawType)
    {
        return rawType?.Trim() switch
        {
            "Boolean" => "Логический",
            "Number" => "Число",
            "Text" => "Текст",
            "SingleSelect" => "Одиночный выбор",
            "MultiSelect" => "Множественный выбор",
            _ => "Не определен"
        };
    }

    public static string HealthStatus(string? rawStatus)
    {
        return rawStatus?.Trim().ToLowerInvariant() switch
        {
            "ok" => "OK",
            "degraded" => "Ошибка",
            "unavailable" => "Недоступно",
            "disabled" => "Отключено",
            _ => "Неизвестно"
        };
    }

    public static string HealthBadgeClass(string? rawStatus)
    {
        return rawStatus?.Trim().ToLowerInvariant() switch
        {
            "ok" => "bg-success-subtle text-success-emphasis",
            "disabled" => "bg-secondary-subtle text-secondary-emphasis",
            "unavailable" => "bg-danger-subtle text-danger-emphasis",
            "degraded" => "bg-danger-subtle text-danger-emphasis",
            _ => "bg-warning-subtle text-warning-emphasis"
        };
    }

    public static string RealtorRequestStatus(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Pending" => "Ожидает решения",
            "Approved" => "Одобрена",
            "Rejected" => "Отклонена",
            _ => "Неизвестно"
        };
    }

    public static string RealtorLevel(string? rawLevel)
    {
        return rawLevel?.Trim() switch
        {
            "Junior" => "Junior",
            "Standard" => "Standard",
            "Top" => "Top",
            _ => "Не указан"
        };
    }

    public static string RealtorRequestBadgeClass(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Pending" => "bg-warning-subtle text-warning-emphasis",
            "Approved" => "bg-success-subtle text-success-emphasis",
            "Rejected" => "bg-danger-subtle text-danger-emphasis",
            _ => "bg-secondary-subtle text-secondary-emphasis"
        };
    }
}
