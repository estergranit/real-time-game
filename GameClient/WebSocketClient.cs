using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using Shared;

namespace GameClient;

public class WebSocketClient : IDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly string _serverUri;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    
    public bool IsConnected => _webSocket.State == WebSocketState.Open;
    public string? CurrentPlayerId { get; private set; }
    
    public event EventHandler<MessageEnvelope>? MessageReceived;
    
    public WebSocketClient(string serverUri = "ws://localhost:8080/ws")
    {
        _webSocket = new ClientWebSocket();
        _serverUri = serverUri;
    }
    
    public async Task ConnectAsync()
    {
        try
        {
            await _webSocket.ConnectAsync(new Uri(_serverUri), CancellationToken.None);
            Log.Information("Connected to server at {ServerUri}", _serverUri);
            
            // Start receive loop
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to server");
            throw;
        }
    }
    
    public async Task<MessageEnvelope> SendMessageAsync(MessageEnvelope envelope)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server");
        }
        
        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
        
        Log.Debug("Sent message: {MessageType}", envelope.Type);
        
        // Wait for response (simplified - assumes synchronous request/response)
        return await WaitForResponseAsync(envelope.RequestId, TimeSpan.FromSeconds(5));
    }
    
    private readonly Dictionary<string?, TaskCompletionSource<MessageEnvelope>> _pendingRequests = new();
    
    private async Task<MessageEnvelope> WaitForResponseAsync(string? requestId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<MessageEnvelope>();
        
        lock (_pendingRequests)
        {
            _pendingRequests[requestId] = tcs;
        }
        
        var timeoutTask = Task.Delay(timeout);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
        
        lock (_pendingRequests)
        {
            _pendingRequests.Remove(requestId);
        }
        
        if (completedTask == timeoutTask)
        {
            throw new TimeoutException("Request timed out waiting for response");
        }
        
        return await tcs.Task;
    }
    
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server closed connection",
                        CancellationToken.None);
                    Log.Information("Server closed connection");
                    break;
                }
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Log.Debug("Received message: {Message}", json);
                    
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(json);
                        if (envelope != null)
                        {
                            HandleReceivedMessage(envelope);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, "Failed to parse received message");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Receive loop cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in receive loop");
        }
    }
    
    private void HandleReceivedMessage(MessageEnvelope envelope)
    {
        // Special handling for login response to set CurrentPlayerId
        if (envelope.Type == MessageType.LoginResponse)
        {
            var response = JsonSerializer.Deserialize<LoginResponse>(envelope.Payload);
            if (response?.Success == true)
            {
                CurrentPlayerId = response.PlayerId;
            }
        }
        
        // Check if this is a response to a pending request
        lock (_pendingRequests)
        {
            if (_pendingRequests.TryGetValue(envelope.RequestId, out var tcs))
            {
                tcs.SetResult(envelope);
                return;
            }
        }
        
        // Handle unsolicited messages (like GiftEvent)
        MessageReceived?.Invoke(this, envelope);
    }
    
    public async Task DisconnectAsync()
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            _receiveCts?.Cancel();
            
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnecting",
                CancellationToken.None);
            
            if (_receiveTask != null)
            {
                await _receiveTask;
            }
            
            Log.Information("Disconnected from server");
        }
    }
    
    public void Dispose()
    {
        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _webSocket.Dispose();
    }
}

