using System.Net.WebSockets;
using Shared;

namespace GameServer;

public class PlayerState
{
    private readonly object _lock = new();
    
    public string PlayerId { get; }
    public string DeviceId { get; }
    public WebSocket? WebSocket { get; set; }
    
    private int _coins;
    private int _rolls;
    
    public PlayerState(string playerId, string deviceId)
    {
        PlayerId = playerId;
        DeviceId = deviceId;
        _coins = 0;
        _rolls = 0;
    }
    
    public int GetBalance(ResourceType resourceType)
    {
        lock (_lock)
        {
            return resourceType switch
            {
                ResourceType.Coins => _coins,
                ResourceType.Rolls => _rolls,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };
        }
    }
    
    public bool TryUpdateBalance(ResourceType resourceType, int delta, out int newBalance)
    {
        lock (_lock)
        {
            var currentBalance = resourceType switch
            {
                ResourceType.Coins => _coins,
                ResourceType.Rolls => _rolls,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };
            
            var calculatedBalance = currentBalance + delta;
            
            // Reject if balance would go negative
            if (calculatedBalance < 0)
            {
                newBalance = currentBalance;
                return false;
            }
            
            // Update the balance
            switch (resourceType)
            {
                case ResourceType.Coins:
                    _coins = calculatedBalance;
                    break;
                case ResourceType.Rolls:
                    _rolls = calculatedBalance;
                    break;
            }
            
            newBalance = calculatedBalance;
            return true;
        }
    }
}

