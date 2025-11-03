using GameServer;
using GameServer.MessageHandlers;
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
builder.Services.AddScoped<ILoginHandler, LoginHandler>();
builder.Services.AddScoped<IResourceHandler, ResourceHandler>();
builder.Services.AddScoped<IGiftHandler, GiftHandler>();
builder.Services.AddScoped<IMessageRouter, MessageRouter>();
builder.Services.AddScoped<WebSocketHandler>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseWebSockets();

// WebSocket endpoint at /ws
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var scope = context.RequestServices.CreateScope();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = scope.ServiceProvider.GetRequiredService<WebSocketHandler>();
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
