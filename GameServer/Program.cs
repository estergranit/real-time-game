using GameServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .WriteTo.Console()
        .WriteTo.File("logs/gameserver-.txt", rollingInterval: RollingInterval.Day)
        .MinimumLevel.Information());

// Configure Kestrel to listen on port 8080
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8080);
});

// Register services
builder.Services.AddSingleton<ConnectionManager>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseWebSockets();

// WebSocket endpoint at /ws
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionManager = context.RequestServices.GetRequiredService<ConnectionManager>();
        var handler = new WebSocketHandler(connectionManager);
        await handler.HandleConnectionAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

app.MapGet("/", () => "Game Server is running. Connect via WebSocket at ws://localhost:8080/ws");

Log.Information("Game Server starting on http://localhost:8080");

app.Run();
