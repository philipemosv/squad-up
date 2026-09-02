using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace SquadUp.ServiceDefaults;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    internal const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetTag("squadup.correlation_id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(StringValues values)
    {
        if (values.Count == 1)
        {
            var candidate = values[0];
            if (candidate is not null && IsValid(candidate))
            {
                return candidate;
            }
        }

        var traceId = Activity.Current?.TraceId ?? default;
        return traceId != default ? traceId.ToString() : Guid.NewGuid().ToString("N");
    }

    private static bool IsValid(string value)
    {
        if (value.Length is 0 or > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')
            {
                return false;
            }
        }

        return true;
    }
}
