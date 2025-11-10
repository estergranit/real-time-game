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
    private bool _disposed;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;
    public string? CurrentPlayerId { get; private set; }

    public event EventHandler<MessageEnvelope>? MessageReceived;

    private readonly Dictionary<string?, TaskCompletionSource<MessageEnvelope>> _pendingRequests = new();

    public WebSocketClient(string serverUri = "ws://localhost:8080/ws")
    {
        _webSocket = new ClientWebSocket();
        _serverUri = serverUri;
        Log.Information("WebSocketClient initialized for {ServerUri}", _serverUri);
    }

    public async Task ConnectAsync()
    {
        ThrowIfDisposed();

        if (IsConnected)
        {
            Log.Warning("ConnectAsync called while client is already connected");
            return;
        }

        try
        {
            Log.Information("Attempting to connect to {ServerUri}", _serverUri);
            await _webSocket.ConnectAsync(new Uri(_serverUri), CancellationToken.None);
            Log.Information("Connected to server at {ServerUri}", _serverUri);

            // לוודא שאין CTS ישן
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();

            _receiveCts = new CancellationTokenSource();
            Log.Debug("Receive CTS created (Token ID: {TokenHash})", _receiveCts.GetHashCode());

            _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
            Log.Information("Receive loop started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to server at {ServerUri}", _serverUri);
            throw;
        }
    }

    public async Task<MessageEnvelope> SendMessageAsync(MessageEnvelope envelope)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            throw new InvalidOperationException("Cannot send message - not connected to server");
        }

        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);

        Log.Debug("Sending message: {Type} ({RequestId})", envelope.Type, envelope.RequestId);

        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: CancellationToken.None);

        return await WaitForResponseAsync(envelope.RequestId, TimeSpan.FromSeconds(5));
    }

    private async Task<MessageEnvelope> WaitForResponseAsync(string? requestId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Log.Warning("Timeout waiting for response (RequestId: {RequestId})", requestId);
            throw new TimeoutException($"Request {requestId} timed out waiting for response");
        }

        Log.Debug("Response received for RequestId {RequestId}", requestId);
        return await tcs.Task;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        Log.Information("ReceiveLoopAsync started (Token ID: {TokenHash})", cancellationToken.GetHashCode());

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   _webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Log.Information("Receive loop cancelled (Token ID: {TokenHash})", cancellationToken.GetHashCode());
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log.Information("Server initiated close with status {Status}", result.CloseStatus);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Log.Debug("Raw message received: {Json}", json);

                    try
                    {
                        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(json);
                        if (envelope != null)
                        {
                            Log.Debug("Deserialized message: {Type} ({RequestId})", envelope.Type, envelope.RequestId);
                            HandleReceivedMessage(envelope);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, "Failed to deserialize received message");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Receive loop cancelled normally");
        }
        catch (ObjectDisposedException)
        {
            Log.Debug("Receive loop ended due to disposed WebSocket");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in receive loop");
        }
        finally
        {
            Log.Information("ReceiveLoopAsync ended");
        }
    }

    private void HandleReceivedMessage(MessageEnvelope envelope)
    {
        if (envelope.Type == MessageType.LoginResponse)
        {
            var response = JsonSerializer.Deserialize<LoginResponse>(envelope.Payload);
            if (response?.Success == true)
            {
                CurrentPlayerId = response.PlayerId;
                Log.Information("Player logged in successfully (PlayerId: {PlayerId})", CurrentPlayerId);
            }
        }

        lock (_pendingRequests)
        {
            if (envelope.RequestId != null &&
                _pendingRequests.TryGetValue(envelope.RequestId, out var tcs))
            {
                Log.Debug("Completing pending request {RequestId}", envelope.RequestId);
                _pendingRequests.Remove(envelope.RequestId);
                tcs.TrySetResult(envelope);
                return;
            }
        }

        Log.Debug("Dispatching unsolicited message: {Type}", envelope.Type);
        MessageReceived?.Invoke(this, envelope);
    }

    public async Task DisconnectAsync()
    {
        if (_disposed)
        {
            Log.Warning("DisconnectAsync called on disposed client");
            return;
        }

        if (!IsConnected)
        {
            Log.Information("Client already disconnected");
            return;
        }

        Log.Information("DisconnectAsync initiated");

        _receiveCts?.Cancel();
        Log.Debug("Receive CTS cancelled (Token ID: {TokenHash})", _receiveCts?.GetHashCode());

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
                Log.Information("Receive task completed gracefully");
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Receive task cancelled");
            }
        }

        try
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnecting",
                CancellationToken.None);

            Log.Information("Disconnected from server");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error closing WebSocket");
        }
        finally
        {
            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveTask = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WebSocketClient));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Log.Information("Disposing WebSocketClient...");

        try
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while cancelling CTS during Dispose");
        }

        _webSocket.Dispose();
        Log.Information("WebSocket disposed");
    }
}
