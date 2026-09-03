using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SquadUp.ServiceDefaults.Tests;

public sealed class LobbyServiceDefaultsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public LobbyServiceDefaultsTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReturnsOnlySanitizedStatus(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", document!.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.ToString().Contains("exception", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RootEndpointUsesValidatedLobbyServiceName()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SquadUp.LobbyService", document!.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public void InvalidLobbyServiceNameStopsHostStartup()
    {
        using var invalidFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Lobby:ServiceName"] = "invalid service name"
                    }));
            });

        var exception = Assert.Throws<OptionsValidationException>(() => invalidFactory.CreateClient());

        Assert.Contains("Lobby:ServiceName", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid service name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidOtlpEndpointIsRejectedWithoutEchoingItsValue()
    {
        const string invalidEndpoint = "ftp://collector.internal";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = invalidEndpoint;

        var exception = Assert.Throws<InvalidOperationException>(builder.AddSquadUpServiceDefaults);

        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidEndpoint, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidCorrelationIdIsReturnedToTheCaller()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Correlation-ID", "client-request_123");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("client-request_123", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task InvalidCorrelationIdIsNotReflected()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        var invalidCorrelationId = new string('a', 65);
        request.Headers.Add("X-Correlation-ID", invalidCorrelationId);

        using var response = await client.SendAsync(request);
        var returnedCorrelationId = response.Headers.GetValues("X-Correlation-ID").Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(invalidCorrelationId, returnedCorrelationId);
        Assert.InRange(returnedCorrelationId.Length, 1, 64);
    }

    [Fact]
    public async Task UnhandledExceptionReturnsSanitizedProblemDetails()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.AddSquadUpServiceDefaults();

        await using var app = builder.Build();
        app.UseSquadUpServiceDefaults();
        app.MapGet("/", ThrowUnhandledException);
        await app.StartAsync();

        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.GetProperty("traceId").GetString()?.Length > 0);
        Assert.DoesNotContain("Sensitive exception detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    private static IResult ThrowUnhandledException() =>
        throw new InvalidOperationException("Sensitive exception detail for redaction test.");
}
