using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using Shared;

namespace GameServer;

public class WebSocketHandler
{
    private readonly ConnectionManager _connectionManager;
    private readonly MessageRouter _messageRouter;
    
    public WebSocketHandler(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _messageRouter = new MessageRouter(connectionManager);
    }
    
    public async Task HandleConnectionAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        string? currentPlayerId = null;
        
        try
        {
            Log.Information("WebSocket connection opened");
            
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed by client",
                        CancellationToken.None);
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
                            // Route message to appropriate handler
                            var response = await _messageRouter.RouteMessageAsync(envelope, currentPlayerId);
                            
                            var responseJson = JsonSerializer.Serialize(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(responseBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, "Failed to parse message");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WebSocket error occurred");
        }
        finally
        {
            if (currentPlayerId != null)
            {
                _connectionManager.RemovePlayer(currentPlayerId);
            }
            
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Connection terminated",
                    CancellationToken.None);
            }
            
            Log.Information("WebSocket connection closed");
        }
    }
}

