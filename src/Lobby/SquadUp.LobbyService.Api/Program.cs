using Microsoft.Extensions.Options;
using SquadUp.LobbyService.Api;
using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();
builder.Services
    .AddOptions<LobbyHostOptions>()
    .BindConfiguration(LobbyHostOptions.SectionName)
    .Validate(LobbyHostOptions.IsValid, LobbyHostOptions.ValidationError)
    .ValidateOnStart();

var app = builder.Build();
app.UseSquadUpServiceDefaults();

app.MapGet("/", (IOptions<LobbyHostOptions> options) =>
    Results.Ok(new { Service = options.Value.ServiceName }));
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
