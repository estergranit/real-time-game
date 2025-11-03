using System.Net.WebSockets;
using System.Threading;
using Serilog;
using Shared;

namespace GameServer;

public class PlayerState
{
    private readonly SemaphoreSlim _resourceSemaphore = new(1, 1);
    private readonly SemaphoreSlim _webSocketSemaphore = new(1, 1);
    private WebSocket? _webSocket;
    
    public string PlayerId { get; }
    public string DeviceId { get; }
    
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
    
    /// <summary>
    /// Attempts to get the WebSocket for this player with timeout protection.
    /// Returns (Success, Socket) where Success=false indicates a lock timeout occurred.
    /// </summary>
    public async Task<(bool Success, WebSocket? Socket)> TryGetWebSocketAsync(CancellationToken cancellationToken = default)
    {
        // Use aggressive timeout - reference reads should be instant (microseconds)
        // If we can't acquire in 100ms, something is seriously wrong
        const int timeoutMs = 100;
        if (!await _webSocketSemaphore.WaitAsync(timeoutMs, cancellationToken))
        {
            Log.Warning("Timeout acquiring WebSocket lock (Get) for player {PlayerId} after {TimeoutMs}ms - possible deadlock or extreme contention", 
                PlayerId, timeoutMs);
            return (false, null);
        }
        
        try
        {
            return (true, _webSocket);
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Attempts to set the WebSocket for this player with timeout protection.
    /// Returns true if the lock was acquired and the socket was set; false if timeout occurred.
    /// </summary>
    public async Task<bool> TrySetWebSocketAsync(WebSocket? socket, CancellationToken cancellationToken = default)
    {
        // Use slightly longer timeout for writes (but still very short)
        const int timeoutMs = 200;
        if (!await _webSocketSemaphore.WaitAsync(timeoutMs, cancellationToken))
        {
            Log.Warning("Timeout acquiring WebSocket lock (Set) for player {PlayerId} after {TimeoutMs}ms - possible deadlock", 
                PlayerId, timeoutMs);
            return false;
        }
        
        try
        {
            _webSocket = socket;
            return true;
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }
    
    // Legacy methods kept for backward compatibility - these are convenience wrappers
    [Obsolete("Use TryGetWebSocketAsync() for clearer timeout vs null distinction")]
    public async Task<WebSocket?> GetWebSocketAsync(CancellationToken cancellationToken = default)
    {
        var (success, socket) = await TryGetWebSocketAsync(cancellationToken);
        return socket; // Returns null on both timeout and actual null - ambiguous!
    }
    
    [Obsolete("Use TrySetWebSocketAsync() for clearer error reporting")]
    public async Task<bool> SetWebSocketAsync(WebSocket? socket, CancellationToken cancellationToken = default)
    {
        return await TrySetWebSocketAsync(socket, cancellationToken);
    }
    
    // Synchronous overloads for non-async callers (e.g., LoginHandler reconnection check)
    public WebSocket? GetWebSocket()
    {
        _webSocketSemaphore.Wait();
        try
        {
            return _webSocket;
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }
    
    public void SetWebSocket(WebSocket? socket)
    {
        _webSocketSemaphore.Wait();
        try
        {
            _webSocket = socket;
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }
}

