using GameClient;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/gameclient-.txt", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    Log.Information("Game Client starting...");
    
    using var client = new WebSocketClient("ws://localhost:8080/ws");
    await client.ConnectAsync();
    
    var cli = new CommandLineInterface(client);
    await cli.RunAsync();
    
    await client.DisconnectAsync();
    
    Log.Information("Game Client stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Game Client terminated unexpectedly");
    Console.WriteLine($"Fatal error: {ex.Message}");
}
finally
{
    Log.CloseAndFlush();
}
