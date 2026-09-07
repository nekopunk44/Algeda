using AppValidationException = Application.Exceptions.ValidationException;
using Application.Exceptions;
using Domain.Common;
using FluentValidationException = FluentValidation.ValidationException;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Net.Sockets;

namespace API.Exceptions;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Необработанное исключение для запроса {Path}", httpContext.Request.Path);
        }
        else if (exception is not AppValidationException
                 && exception is not FluentValidationException
                 && exception is not DomainException
                 && exception is not NotFoundException
                 && exception is not ConflictException)
        {
            logger.LogWarning(exception, "Неожиданная ошибка запроса {Path}", httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (exception is FluentValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(x => x.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        if (IsDatabaseConnectionException(exception))
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "Сервис временно недоступен",
                "Данные временно недоступны. Попробуйте обновить страницу позже.");
        }

        return exception switch
        {
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Ресурс не найден",
                exception.Message),

            ConflictException => (
                StatusCodes.Status409Conflict,
                "Конфликт",
                exception.Message),

            AppValidationException or FluentValidationException or DomainException or BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Ошибка валидации",
                exception.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера",
                "На сервере произошла ошибка. Попробуйте позже.")
        };
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
