using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using Shared;

namespace GameServer.MessageHandlers;

public class LoginHandler : ILoginHandler
{
    private readonly ConnectionManager _connectionManager;
    
    public LoginHandler(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
    
    public async Task<LoginResponse> HandleAsync(LoginRequest request, WebSocket webSocket)
    {
        await Task.CompletedTask; // Ensure async signature
        
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            Log.Warning("Login attempt with empty DeviceId");
            return new LoginResponse
            {
                Success = false,
                PlayerId = string.Empty
            };
        }
        
        // Check if device already has a player (reconnection scenario)
        var existingPlayer = _connectionManager.GetPlayerByDeviceId(request.DeviceId);
        
        if (existingPlayer != null)
        {
            // Reconnection: update WebSocket and return existing PlayerId
            var existingSocket = existingPlayer.GetWebSocket();
            if (existingSocket != null && existingSocket.State == System.Net.WebSockets.WebSocketState.Open)
            {
                // Player is already connected with an active WebSocket
                Log.Warning("Login rejected - DeviceId already connected: {DeviceId}", request.DeviceId);
                return new LoginResponse
                {
                    Success = false,
                    PlayerId = string.Empty
                };
            }
            
            // Player was disconnected, reconnect them
            var setSuccess = await existingPlayer.TrySetWebSocketAsync(webSocket);
            if (!setSuccess)
            {
                Log.Warning("Failed to reconnect player {PlayerId} - lock timeout when setting WebSocket", existingPlayer.PlayerId);
                return new LoginResponse
                {
                    Success = false,
                    PlayerId = string.Empty
                };
            }
            Log.Information("Player reconnected: {PlayerId} with DeviceId: {DeviceId}", existingPlayer.PlayerId, request.DeviceId);
            
            return new LoginResponse
            {
                Success = true,
                PlayerId = existingPlayer.PlayerId
            };
        }
        
        // Create new player
        var playerId = _connectionManager.CreatePlayer(request.DeviceId, webSocket);
        
        Log.Information("Player logged in: {PlayerId} with DeviceId: {DeviceId}", playerId, request.DeviceId);
        
        return new LoginResponse
        {
            Success = true,
            PlayerId = playerId
        };
    }
}

