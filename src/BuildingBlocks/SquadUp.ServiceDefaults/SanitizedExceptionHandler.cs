using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SquadUp.ServiceDefaults;

internal sealed class SanitizedExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<SanitizedExceptionHandler> logger) : IExceptionHandler
{
    private static readonly EventId UnhandledRequestException = new(
        1000,
        nameof(UnhandledRequestException));
    private static readonly Action<ILogger, string, Exception?> LogUnhandledRequestException =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            UnhandledRequestException,
            "Unhandled request failed with exception type {ExceptionType}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledRequestException(logger, exception.GetType().Name, null);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        });
    }
}
