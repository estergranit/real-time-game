using System.Net.WebSockets;
using System.Threading;
using Shared;

namespace GameServer;

public class PlayerState
{
    private readonly SemaphoreSlim _resourceSemaphore = new(1, 1);
    
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
    
    public async Task<int> GetBalanceAsync(ResourceType resourceType)
    {
        await _resourceSemaphore.WaitAsync();
        try
        {
            return resourceType switch
            {
                ResourceType.Coins => _coins,
                ResourceType.Rolls => _rolls,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };
        }
        finally
        {
            _resourceSemaphore.Release();
        }
    }
    
    public async Task<(bool Success, int NewBalance)> TryUpdateBalanceAsync(ResourceType resourceType, int delta)
    {
        await _resourceSemaphore.WaitAsync();
        try
        {
            var currentBalance = resourceType switch
            {
                ResourceType.Coins => _coins,
                ResourceType.Rolls => _rolls,
                _ => throw new ArgumentException($"Unknown resource type: {resourceType}")
            };

            var calculatedBalance = currentBalance + delta;

            if (calculatedBalance < 0)
            {
                return (false, currentBalance);
            }

            switch (resourceType)
            {
                case ResourceType.Coins:
                    _coins = calculatedBalance;
                    break;
                case ResourceType.Rolls:
                    _rolls = calculatedBalance;
                    break;
            }

            return (true, calculatedBalance);
        }
        finally
        {
            _resourceSemaphore.Release();
        }
    }

    internal SemaphoreSlim ResourceSemaphore => _resourceSemaphore;
}

