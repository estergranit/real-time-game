using Serilog;
using Shared;

namespace GameServer.MessageHandlers;

public class ResourceHandler : IResourceHandler
{
    private readonly ConnectionManager _connectionManager;
    
    public ResourceHandler(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
    
    public async Task<UpdateResourcesResponse> HandleAsync(UpdateResourcesRequest request)
    {
        // Validate resource value is positive
        if (request.ResourceValue <= 0)
        {
            Log.Warning("UpdateResources rejected - non-positive value: {Value}", request.ResourceValue);
            return new UpdateResourcesResponse
            {
                Success = false,
                ResourceType = request.ResourceType,
                NewBalance = 0
            };
        }
        
        // Find player
        var player = _connectionManager.GetPlayerByPlayerId(request.PlayerId);
        if (player == null)
        {
            Log.Warning("UpdateResources failed - player not found: {PlayerId}", request.PlayerId);
            return new UpdateResourcesResponse
            {
                Success = false,
                ResourceType = request.ResourceType,
                NewBalance = 0
            };
        }
        
        // Try to update balance (will reject if would go negative)
        var (success, newBalance) = await player.TryUpdateBalanceAsync(request.ResourceType, request.ResourceValue);
        
        if (!success)
        {
            Log.Warning("UpdateResources rejected - would result in negative balance for {PlayerId}", request.PlayerId);
            return new UpdateResourcesResponse
            {
                Success = false,
                ResourceType = request.ResourceType,
                NewBalance = newBalance // Returns current balance
            };
        }
        
        Log.Information("UpdateResources success - {PlayerId}: {ResourceType} += {Value}, new balance: {NewBalance}",
            request.PlayerId, request.ResourceType, request.ResourceValue, newBalance);
        
        return new UpdateResourcesResponse
        {
            Success = true,
            ResourceType = request.ResourceType,
            NewBalance = newBalance
        };
    }
}

