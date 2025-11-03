using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using Shared;

namespace GameServer;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<string, PlayerState> _playersByDeviceId = new();
    private readonly ConcurrentDictionary<string, PlayerState> _playersByPlayerId = new();
    private int _playerIdCounter = 0;
    
    public bool IsDeviceConnected(string deviceId)
    {
        return _playersByDeviceId.ContainsKey(deviceId);
    }
    
    public string CreatePlayer(string deviceId, WebSocket webSocket)
    {
        var playerId = $"player_{Interlocked.Increment(ref _playerIdCounter)}";
        var playerState = new PlayerState(playerId, deviceId);
        // Use synchronous SetWebSocket during initialization - no contention possible yet
        playerState.SetWebSocket(webSocket);
        
        _playersByDeviceId[deviceId] = playerState;
        _playersByPlayerId[playerId] = playerState;
        
        Log.Information("Player created: {PlayerId} for device {DeviceId}", playerId, deviceId);
        return playerId;
    }
    
    public PlayerState? GetPlayerByPlayerId(string playerId)
    {
        _playersByPlayerId.TryGetValue(playerId, out var player);
        return player;
    }
    
    public PlayerState? GetPlayerByDeviceId(string deviceId)
    {
        _playersByDeviceId.TryGetValue(deviceId, out var player);
        return player;
    }
    
    public void RemovePlayer(string playerId)
    {
        if (_playersByPlayerId.TryRemove(playerId, out var playerState))
        {
            _playersByDeviceId.TryRemove(playerState.DeviceId, out _);
            Log.Information("Player removed: {PlayerId}", playerId);
        }
    }
    
    public async Task SendMessageAsync(PlayerState player, MessageEnvelope envelope)
    {
        var (success, webSocket) = await player.TryGetWebSocketAsync();
        if (!success)
        {
            Log.Warning("Cannot send message to {PlayerId} - failed to acquire WebSocket lock", player.PlayerId);
            return;
        }
        
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Log.Warning("Cannot send message to {PlayerId} - WebSocket not open", player.PlayerId);
            return;
        }
        
        try
        {
            var json = JsonSerializer.Serialize(envelope);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            
            Log.Debug("Message sent to {PlayerId}: {MessageType}", player.PlayerId, envelope.Type);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending message to {PlayerId}", player.PlayerId);
        }
    }
}

