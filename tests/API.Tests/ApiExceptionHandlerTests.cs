using API.Exceptions;
using AppValidationException = Application.Exceptions.ValidationException;
using Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace API.Tests;

public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ShouldMapNotFoundExceptionTo404()
    {
        var context = CreateHttpContext();
        var handler = CreateHandler();
        var exception = new NotFoundException("Requirement was not found.");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        var problem = await ReadProblemDetails(context);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal(exception.Message, problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapValidationExceptionTo400()
    {
        var context = CreateHttpContext();
        var handler = CreateHandler();
        var exception = new AppValidationException("Request validation failed.");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        var problem = await ReadProblemDetails(context);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(exception.Message, problem.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapConflictExceptionTo409()
    {
        var context = CreateHttpContext();
        var handler = CreateHandler();
        var exception = new ConflictException("Resource conflict.");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        var problem = await ReadProblemDetails(context);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal(exception.Message, problem.Detail);
    }

    private static ApiExceptionHandler CreateHandler()
    {
        return new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ProblemDetails> ReadProblemDetails(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Assert.NotNull(problem);
        return problem;
    }
}
