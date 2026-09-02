using SquadUp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddSquadUpServiceDefaults();

var app = builder.Build();
app.UseSquadUpServiceDefaults();

app.MapGet("/", () => Results.Ok(new { Service = "SquadUp.LobbyService" }));
app.MapSquadUpHealthEndpoints();

app.Run();

public partial class Program;
