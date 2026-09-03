using SquadUp.Api;
using SquadUp.Identity.Infrastructure;
using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddDiscordOAuth(builder.Configuration);

var app = builder.Build();
app.UseSquadUpServiceDefaults();
app.UseAuthentication();

app.MapGet("/", () => Results.Ok(new { Service = "SquadUp.Api" }));
app.MapDiscordAuthentication();
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
