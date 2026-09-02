namespace Web.Models.Api
{
    public static class ApiErrorFactory
    {
        public static ApiErrorViewModel Create(int statusCode, string? detail = null)
        {
            detail = NormalizeDetail(detail);

            return statusCode switch
            {
                StatusCodes.Status401Unauthorized => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Требуется авторизация",
                    Message = detail ?? "Сессия недействительна или истекла. Выполните вход снова."
                },
                StatusCodes.Status403Forbidden => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Нет доступа",
                    Message = detail ?? "У вас недостаточно прав для этой операции."
                },
                StatusCodes.Status423Locked => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Аккаунт заморожен",
                    Message = detail ?? "Ваш аккаунт заморожен. Обратитесь к администратору."
                },
                StatusCodes.Status404NotFound => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Не найдено",
                    Message = detail ?? "Запрошенный ресурс не найден."
                },
                StatusCodes.Status409Conflict => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Конфликт данных",
                    Message = detail ?? "Операцию нельзя выполнить из-за конфликта состояния данных."
                },
                StatusCodes.Status500InternalServerError => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Ошибка сервера",
                    Message = detail ?? "На сервере произошла ошибка. Повторите попытку позже."
                },
                StatusCodes.Status502BadGateway => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Сервер временно недоступен",
                    Message = detail ?? "Не удалось связаться с сервером. Попробуйте еще раз позже."
                },
                StatusCodes.Status503ServiceUnavailable => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Сервер временно недоступен",
                    Message = detail ?? "Не удалось связаться с сервером. Попробуйте еще раз позже."
                },
                StatusCodes.Status504GatewayTimeout => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Сервер отвечает слишком долго",
                    Message = detail ?? "Сервер не успел ответить. Попробуйте еще раз."
                },
                _ => new ApiErrorViewModel
                {
                    StatusCode = statusCode,
                    Title = "Ошибка сервера",
                    Message = detail ?? "Не удалось выполнить запрос. Попробуйте еще раз."
                }
            };
        }

        private static string? NormalizeDetail(string? detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return null;
            }

            var value = detail.Trim();
            return value switch
            {
                _ when IsUnsafeDetail(value) => null,
                _ when value.Contains("One or more validation errors occurred", StringComparison.OrdinalIgnoreCase)
                    => "Проверьте заполнение формы.",
                _ when value.Equals("Bad Request", StringComparison.OrdinalIgnoreCase)
                    => "Проверьте заполнение формы.",
                _ when value.Equals("Unauthorized", StringComparison.OrdinalIgnoreCase)
                    => "Сессия недействительна или истекла. Выполните вход снова.",
                _ when value.Equals("Forbidden", StringComparison.OrdinalIgnoreCase)
                    => "У вас недостаточно прав для этой операции.",
                _ when value.Equals("Not Found", StringComparison.OrdinalIgnoreCase)
                    => "Запрошенные данные не найдены.",
                _ when value.Equals("Internal Server Error", StringComparison.OrdinalIgnoreCase)
                    => "На сервере произошла ошибка. Повторите попытку позже.",
                _ when value.Contains("HttpClient.Timeout", StringComparison.OrdinalIgnoreCase)
                    => "Сервер отвечает слишком долго. Попробуйте еще раз.",
                _ when value.Contains("task was canceled", StringComparison.OrdinalIgnoreCase)
                    => "Сервер отвечает слишком долго. Попробуйте еще раз.",
                _ => value
            };
        }

        private static bool IsUnsafeDetail(string value)
        {
            return value.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || value.Contains("<head", StringComparison.OrdinalIgnoreCase)
                || value.Contains("<body", StringComparison.OrdinalIgnoreCase)
                || value.Contains("</html", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Render", StringComparison.OrdinalIgnoreCase)
                || value.Contains("nginx", StringComparison.OrdinalIgnoreCase);
        }
    }
}
