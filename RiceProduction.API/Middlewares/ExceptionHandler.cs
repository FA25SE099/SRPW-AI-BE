using Microsoft.AspNetCore.Diagnostics;
using RiceProduction.Application.Common.Exceptions;
using RiceProduction.Application.Common.Models;

namespace RiceProduction.API.Middlewares;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                Result<object>.Failure(
                    validationEx.Errors.SelectMany(kvp => kvp.Value).ToArray(),
                    validationEx.Message)),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                Result<object>.Failure("Unauthorized access.", "Unauthorized")),

            ForbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                Result<object>.Failure("You do not have permission to perform this action.", "Forbidden")),

            KeyNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                Result<object>.Failure(notFoundEx.Message, "NotFound")),

            ArgumentException or InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                Result<object>.Failure(exception.Message, "BadRequest")),

            _ => (
                StatusCodes.Status500InternalServerError,
                Result<object>.Failure("An unexpected error occurred.", "InternalServerError"))
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with status {StatusCode} on {Method} {Path}",
                statusCode, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
