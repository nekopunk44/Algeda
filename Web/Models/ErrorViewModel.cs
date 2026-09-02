namespace Web.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public string Title { get; set; } = "Ошибка";

        public string Heading { get; set; } = "Не удалось обработать запрос.";

        public string Message { get; set; } = "Попробуйте повторить действие позже. Если ошибка повторится, обратитесь к администратору.";

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
