using Microsoft.Extensions.Options;
using SquadUp.LobbyService.Api;
using SquadUp.LobbyService.Infrastructure;
using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();
builder.Services
    .AddOptions<LobbyHostOptions>()
    .BindConfiguration(LobbyHostOptions.SectionName)
    .Validate(LobbyHostOptions.IsValid, LobbyHostOptions.ValidationError)
    .ValidateOnStart();
builder.Services.AddLobbyInternalAuthentication(builder.Configuration);
builder.Services.AddLobbyPersistence(builder.Configuration);

var app = builder.Build();
app.UseSquadUpServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (IOptions<LobbyHostOptions> options) =>
    Results.Ok(new { Service = options.Value.ServiceName }));
app.MapLobby();
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
