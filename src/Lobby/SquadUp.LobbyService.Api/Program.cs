var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "SquadUp.LobbyService" }));

app.Run();
