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
        
        // Check if device is already connected
        if (_connectionManager.IsDeviceConnected(request.DeviceId))
        {
            Log.Warning("Login rejected - DeviceId already connected: {DeviceId}", request.DeviceId);
            return new LoginResponse
            {
                Success = false,
                PlayerId = string.Empty
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

