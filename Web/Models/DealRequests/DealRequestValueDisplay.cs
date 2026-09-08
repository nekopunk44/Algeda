namespace Web.Models.DealRequests;

public static class DealRequestValueDisplay
{
    public static string Source(string? rawSource)
    {
        return rawSource?.Trim() switch
        {
            "Home" => "Главная",
            "Matching" => "Подбор",
            "Manual" => "Вручную",
            "Sale" => "Продажа",
            _ => "Не указано"
        };
    }

    public static string Status(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Created" => "Входящая",
            "InProgress" => "В работе",
            "Completed" => "Завершена",
            "Cancelled" => "Отменена",
            _ => "Не указано"
        };
    }

    public static string StatusBadgeClass(string? rawStatus)
    {
        return rawStatus?.Trim() switch
        {
            "Created" => "bg-warning-subtle text-warning-emphasis border",
            "InProgress" => "bg-primary-subtle text-primary-emphasis border",
            "Completed" => "bg-success-subtle text-success-emphasis border",
            "Cancelled" => "bg-secondary-subtle text-secondary-emphasis border",
            _ => "bg-light text-dark border"
        };
    }

    public static string RequirementPriority(string? rawPriority)
    {
        return rawPriority?.Trim() switch
        {
            "MustHave" => "Обязательно",
            "Important" => "Важно",
            "NiceToHave" => "Желательно",
            _ => "Не указано"
        };
    }

    public static string SaleLifecycleStage(string? rawStage)
    {
        return rawStage?.Trim() switch
        {
            "Submitted" => "Заявка отправлена",
            "SearchingBuyer" => "Поиск покупателя",
            "BuyerFound" => "Найден покупатель",
            "DealCompleted" => "Сделка завершена",
            "Cancelled" => "Отменена",
            _ => "Не указано"
        };
    }

    public static string SaleLifecycleBadgeClass(string? rawStage)
    {
        return rawStage?.Trim() switch
        {
            "Submitted" => "bg-warning-subtle text-warning-emphasis border",
            "SearchingBuyer" => "bg-primary-subtle text-primary-emphasis border",
            "BuyerFound" => "bg-info-subtle text-info-emphasis border",
            "DealCompleted" => "bg-success-subtle text-success-emphasis border",
            "Cancelled" => "bg-secondary-subtle text-secondary-emphasis border",
            _ => "bg-light text-dark border"
        };
    }
}
