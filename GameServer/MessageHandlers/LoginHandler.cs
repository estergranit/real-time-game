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
            // Reconnection: atomically check if socket is null/closed and set it
            // This prevents race conditions where multiple threads try to reconnect simultaneously
            var (lockSuccess, wasSet) = await existingPlayer.TrySetWebSocketIfNullOrClosedAsync(webSocket);
            
            if (!lockSuccess)
            {
                Log.Warning("Failed to reconnect player {PlayerId} - lock timeout when checking/setting WebSocket", existingPlayer.PlayerId);
                return new LoginResponse
                {
                    Success = false,
                    PlayerId = string.Empty
                };
            }
            
            if (!wasSet)
            {
                // Socket was already open - another thread connected first or player is already connected
                Log.Warning("Login rejected - DeviceId already connected: {DeviceId}", request.DeviceId);
                return new LoginResponse
                {
                    Success = false,
                    PlayerId = string.Empty
                };
            }
            
            // Successfully reconnected
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

