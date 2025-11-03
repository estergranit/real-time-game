using System.Net.WebSockets;
using System.Diagnostics;
using GameServer;
using GameServer.MessageHandlers;
using Shared;
using Serilog;

// Test runner - use command line args: "gift" or "websocket" (default: gift)

var testName = args.Length > 0 ? args[0].ToLower() : "gift";

if (testName == "websocket" || testName == "ws")
{
    await RunWebSocketConcurrencyTest();
}
else
{
    await RunGiftConcurrencyTest();
}

static async Task RunWebSocketConcurrencyTest()
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .MinimumLevel.Warning()
        .CreateLogger();

    var connectionManager = new ConnectionManager();
    var testResults = new WebSocketTestResults();

    Console.WriteLine("==========================================");
    Console.WriteLine("WebSocket Concurrency Stress Test");
    Console.WriteLine("==========================================");
    Console.WriteLine();

    // Create test players with dummy sockets
    var players = new List<(string PlayerId, PlayerState State)>();
    for (int i = 0; i < 10; i++)
    {
        var dummySocket = new DummySocket();
        var playerId = connectionManager.CreatePlayer($"device_test_{i}", dummySocket);
        var player = connectionManager.GetPlayerByPlayerId(playerId)!;
        players.Add((playerId, player));
    }

    Console.WriteLine($"Created {players.Count} test players");
    Console.WriteLine();

    // Test configuration
    const int concurrentTasksPerPlayer = 50;
    const int iterationsPerTask = 100;

    Console.WriteLine($"Test Configuration:");
    Console.WriteLine($"  - Players: {players.Count}");
    Console.WriteLine($"  - Concurrent tasks per player: {concurrentTasksPerPlayer}");
    Console.WriteLine($"  - Iterations per task: {iterationsPerTask}");
    Console.WriteLine();

    var sw = Stopwatch.StartNew();
    var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;

    // Spawn concurrent tasks - mix of Get and Set operations
    var tasks = new List<Task>();

    foreach (var (playerId, player) in players)
    {
        // Create reader tasks (Get operations)
        for (int i = 0; i < concurrentTasksPerPlayer / 2; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var localSuccessCount = 0;
                var localTimeoutCount = 0;

                for (int iteration = 0; iteration < iterationsPerTask; iteration++)
                {
                    var (success, socket) = await player.TryGetWebSocketAsync(cancellationToken);
                    if (success)
                    {
                        Interlocked.Increment(ref localSuccessCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref localTimeoutCount);
                    }

                    // Small random delay to increase contention
                    if (iteration % 10 == 0)
                    {
                        await Task.Delay(Random.Shared.Next(0, 2), cancellationToken);
                    }
                }

                Interlocked.Add(ref testResults.SuccessfulGets, localSuccessCount);
                Interlocked.Add(ref testResults.TimeoutGets, localTimeoutCount);
            }, cancellationToken));
        }

        // Create writer tasks (Set operations)
        for (int i = 0; i < concurrentTasksPerPlayer / 2; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var localSuccessCount = 0;
                var localTimeoutCount = 0;
                var dummySocket = new DummySocket();

                for (int iteration = 0; iteration < iterationsPerTask; iteration++)
                {
                    var socketToSet = (iteration % 2 == 0) ? dummySocket : (WebSocket?)null;

                    var success = await player.TrySetWebSocketAsync(socketToSet, cancellationToken);
                    if (success)
                    {
                        Interlocked.Increment(ref localSuccessCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref localTimeoutCount);
                    }

                    if (iteration % 10 == 0)
                    {
                        await Task.Delay(Random.Shared.Next(0, 2), cancellationToken);
                    }
                }

                Interlocked.Add(ref testResults.SuccessfulSets, localSuccessCount);
                Interlocked.Add(ref testResults.TimeoutSets, localTimeoutCount);
            }, cancellationToken));
        }
    }

    Console.WriteLine($"Started {tasks.Count} concurrent tasks...");
    Console.WriteLine("Running...");
    Console.WriteLine();

    await Task.WhenAll(tasks);
    sw.Stop();

    Console.WriteLine("All tasks completed!");
    Console.WriteLine();

    // Final verification
    Console.WriteLine("Final Verification:");
    foreach (var (playerId, player) in players)
    {
        var (success, socket) = await player.TryGetWebSocketAsync();
        if (!success)
        {
            testResults.FinalCheckFailures++;
            Console.WriteLine($"  ✗ Player {playerId}: Failed to acquire lock");
        }
        else
        {
            Console.WriteLine($"  ✓ Player {playerId}: Lock acquired (socket: {(socket != null ? "present" : "null")})");
        }
    }

    Console.WriteLine();
    Console.WriteLine("==========================================");
    Console.WriteLine("Test Results");
    Console.WriteLine("==========================================");
    Console.WriteLine($"Elapsed Time: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalSeconds:F2}s)");
    Console.WriteLine();
    Console.WriteLine($"Successful Gets:  {testResults.SuccessfulGets:N0}");
    Console.WriteLine($"Timeout Gets:     {testResults.TimeoutGets:N0}");
    Console.WriteLine($"Successful Sets:  {testResults.SuccessfulSets:N0}");
    Console.WriteLine($"Timeout Sets:     {testResults.TimeoutSets:N0}");
    Console.WriteLine($"Final Check Failures: {testResults.FinalCheckFailures}");
    Console.WriteLine();

    var totalTimeouts = testResults.TimeoutGets + testResults.TimeoutSets;
    var totalOperations = testResults.SuccessfulGets + testResults.TimeoutGets +
                          testResults.SuccessfulSets + testResults.TimeoutSets;
    var timeoutRate = totalOperations > 0 ? totalTimeouts * 100.0 / totalOperations : 0;

    Console.WriteLine("==========================================");
    if (testResults.FinalCheckFailures == 0 && timeoutRate < 1.0)
    {
        Console.WriteLine("✓ TEST PASSED");
        Console.WriteLine($"  - Timeout rate: {timeoutRate:F3}%");
        Console.WriteLine($"  - No deadlocks detected");
    }
    else if (testResults.FinalCheckFailures == 0 && timeoutRate < 5.0)
    {
        Console.WriteLine("⚠ TEST PASSED WITH WARNINGS");
        Console.WriteLine($"  - Timeout rate: {timeoutRate:F3}%");
    }
    else
    {
        Console.WriteLine("✗ TEST FAILED");
        if (testResults.FinalCheckFailures > 0)
            Console.WriteLine($"  - {testResults.FinalCheckFailures} final check(s) failed");
        if (timeoutRate >= 5.0)
            Console.WriteLine($"  - Timeout rate: {timeoutRate:F3}%");
    }
    Console.WriteLine("==========================================");

    cancellationTokenSource.Dispose();
    Environment.Exit(testResults.FinalCheckFailures == 0 && timeoutRate < 5.0 ? 0 : 1);
}

static async Task RunGiftConcurrencyTest()
{
    var connectionManager = new ConnectionManager();

    var senderSocket = new DummySocket();
    var senderId = connectionManager.CreatePlayer("device_sender", senderSocket);

    var recipients = new List<(string PlayerId, WebSocket Socket)>();
    for (var i = 0; i < 10; i++)
    {
        var ws = new DummySocket();
        var rid = connectionManager.CreatePlayer($"device_recipient_{i}", ws);
        recipients.Add((rid, ws));
    }

    var sender = connectionManager.GetPlayerByPlayerId(senderId)!;
    await sender.TryUpdateBalanceAsync(ResourceType.Coins, 10_000);

    var handler = new GiftHandler(connectionManager);

    var amountPerGift = 100;
    var tasks = recipients.Select(r => handler.HandleAsync(new SendGiftRequest
    {
        SenderId = senderId,
        FriendPlayerId = r.PlayerId,
        ResourceType = ResourceType.Coins,
        ResourceValue = amountPerGift
    })).ToArray();

    var results = await Task.WhenAll(tasks);

    if (results.Any(r => !r.Success))
    {
        Console.WriteLine("FAIL: One or more gifts failed.");
        Environment.Exit(1);
    }

    var expectedSender = 10_000 - (amountPerGift * recipients.Count);
    var actualSender = await sender.GetBalanceAsync(ResourceType.Coins);
    if (actualSender != expectedSender)
    {
        Console.WriteLine($"FAIL: Sender balance mismatch. Expected {expectedSender}, got {actualSender}.");
        Environment.Exit(2);
    }

    foreach (var (playerId, _) in recipients)
    {
        var p = connectionManager.GetPlayerByPlayerId(playerId)!;
        var bal = await p.GetBalanceAsync(ResourceType.Coins);
        if (bal != amountPerGift)
        {
            Console.WriteLine($"FAIL: Recipient {playerId} balance mismatch. Expected {amountPerGift}, got {bal}.");
            Environment.Exit(3);
        }
    }

    Console.WriteLine("PASS: Concurrency gift test succeeded.");
}

class WebSocketTestResults
{
    public long SuccessfulGets;
    public long TimeoutGets;
    public long SuccessfulSets;
    public long TimeoutSets;
    public int FinalCheckFailures;
}

class DummySocket : WebSocket
{
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => WebSocketState.Open;
    public override string? SubProtocol => null;

    public override void Abort() { }
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;
    public override void Dispose() { }
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true));
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
