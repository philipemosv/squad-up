using SquadUp.Api;
using SquadUp.Identity.Infrastructure;
using SquadUp.Profile.Infrastructure;
using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddBrowserSession(builder.Configuration);
builder.Services.AddDiscordOAuth(builder.Configuration);
builder.Services.AddInternalTokenIssuer(builder.Configuration);
builder.Services.AddProfileInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSquadUpServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/", () => Results.Ok(new { Service = "SquadUp.Api" }));
app.MapDiscordAuthentication();
app.MapProfile();
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
