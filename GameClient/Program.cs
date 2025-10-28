using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/gameclient-.txt", rollingInterval: RollingInterval.Day)
    .MinimumLevel.Information()
    .CreateLogger();

try
{
    Log.Information("Game Client starting...");
    Log.Information("Game Client ready. Implementation coming in Phase 8.");
    Log.Information("Connect to: ws://localhost:8080/ws");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Game Client terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
