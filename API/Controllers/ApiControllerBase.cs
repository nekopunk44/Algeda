using Microsoft.AspNetCore.Mvc;
using Application.Exceptions;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;

namespace API.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ошибка валидации",
                Detail = ex.Message
            });
        }
        catch (DomainException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ошибка доменной валидации",
                Detail = ex.Message
            });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Ресурс не найден",
                Detail = ex.Message
            });
        }
        catch (ConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Конфликт",
                Detail = ex.Message
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var logger = HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType());
            logger.LogError(
                ex,
                "Конфликт параллельного обновления для запроса {Path}: {Entries}",
                HttpContext.Request.Path,
                string.Join(", ", ex.Entries.Select(entry => $"{entry.Entity.GetType().Name}/{entry.State}")));

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Конфликт при обновлении",
                Detail = "Запись была изменена или удалена. Обновите данные и повторите операцию."
            });
        }
        catch (Exception ex) when (IsDatabaseConnectionException(ex))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Сервис временно недоступен",
                Detail = "Данные временно недоступны. Попробуйте обновить страницу позже."
            });
        }
    }

    private static bool IsDatabaseConnectionException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException or SocketException or TimeoutException)
            {
                return true;
            }
        }

        return exception.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase);
    }
}
