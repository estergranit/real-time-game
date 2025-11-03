using System.Net.WebSockets;
using GameServer;
using GameServer.MessageHandlers;
using Shared;

// Simple concurrency harness for GiftHandler under in-memory ConnectionManager

var connectionManager = new ConnectionManager();

// Create sender and recipients
var senderSocket = new DummySocket();
var senderId = connectionManager.CreatePlayer("device_sender", senderSocket);

var recipients = new List<(string PlayerId, WebSocket Socket)>();
for (var i = 0; i < 10; i++)
{
    var ws = new DummySocket();
    var rid = connectionManager.CreatePlayer($"device_recipient_{i}", ws);
    recipients.Add((rid, ws));
}

// Prefund sender
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

// Validate all succeeded
if (results.Any(r => !r.Success))
{
    Console.WriteLine("FAIL: One or more gifts failed.");
    Environment.Exit(1);
}

// Validate balances
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

// Lightweight mock WebSocket that doesn't perform network I/O
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
