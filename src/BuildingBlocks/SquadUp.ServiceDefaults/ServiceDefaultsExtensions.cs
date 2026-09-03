using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SquadUp.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddSquadUpServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        var serviceName = builder.Environment.ApplicationName;
        var environmentName = builder.Environment.EnvironmentName;

        AddProblemDetails(builder.Services);
        builder.Services.AddHealthChecks();
        AddJsonLogging(builder, serviceName, environmentName);
        AddOpenTelemetry(builder, serviceName, environmentName);

        return builder;
    }

    public static WebApplication UseSquadUpServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();

        return app;
    }

    public static IEndpointRouteBuilder MapSquadUpHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false,
            ResponseWriter = WriteHealthResponseAsync
        });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        });

        return endpoints;
    }

    private static void AddProblemDetails(IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

                if (context.ProblemDetails.Status >= StatusCodes.Status500InternalServerError)
                {
                    context.ProblemDetails.Detail = null;
                }
            };
        });
        services.AddExceptionHandler<SanitizedExceptionHandler>();
        services.Configure<ExceptionHandlerOptions>(options =>
            options.SuppressDiagnosticsCallback = static _ => true);
    }

    private static void AddJsonLogging(
        IHostApplicationBuilder builder,
        string serviceName,
        string environmentName)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(configuration => configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service_name", serviceName)
            .Enrich.WithProperty("deployment_environment", environmentName)
            .WriteTo.Sink(new RedactingTextWriterSink(
                new RenderedCompactJsonFormatter(),
                Console.Out)));
    }

    private static void AddOpenTelemetry(
        IHostApplicationBuilder builder,
        string serviceName,
        string environmentName)
    {
        var endpoint = GetOtlpEndpoint(builder.Configuration);
        var samplingRatio = builder.Environment.IsDevelopment() ? 1.0 : 0.1;

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment.name", environmentName)
                ]))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddSource(SquadUpTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options => options.RecordException = false)
                    .AddHttpClientInstrumentation(options => options.RecordException = false);

                if (endpoint is not null)
                {
                    tracing.AddOtlpExporter(options => ConfigureExporter(options, endpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(SquadUpTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (endpoint is not null)
                {
                    metrics.AddOtlpExporter(options => ConfigureExporter(options, endpoint));
                }
            });
    }

    private static Uri? GetOtlpEndpoint(IConfiguration configuration)
    {
        var configuredEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "OTEL_EXPORTER_OTLP_ENDPOINT must be an absolute HTTP or HTTPS URI.");
        }

        return endpoint;
    }

    private static void ConfigureExporter(OtlpExporterOptions options, Uri endpoint)
    {
        options.Endpoint = endpoint;
        options.Protocol = OtlpExportProtocol.Grpc;
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)
            })
        };

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            cancellationToken: context.RequestAborted);
    }
}
