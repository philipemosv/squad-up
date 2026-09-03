using SquadUp.Identity.Infrastructure;
using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSquadUpServiceDefaults();

app.MapGet("/", () => Results.Ok(new { Service = "SquadUp.Api" }));
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
