using System.Text.Json;
using Serilog;
using Shared;

namespace GameServer;

public class MessageRouter
{
    private readonly ConnectionManager _connectionManager;
    
    public MessageRouter(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
    
    public async Task<MessageEnvelope> RouteMessageAsync(MessageEnvelope envelope, string? currentPlayerId)
    {
        try
        {
            Log.Information("Routing message type: {MessageType}", envelope.Type);
            
            return envelope.Type switch
            {
                MessageType.Login => await HandleLoginAsync(envelope),
                MessageType.UpdateResources => await HandleUpdateResourcesAsync(envelope, currentPlayerId),
                MessageType.SendGift => await HandleSendGiftAsync(envelope, currentPlayerId),
                _ => CreateErrorResponse("UNKNOWN_MESSAGE_TYPE", 
                    $"Unknown message type: {envelope.Type}", 
                    envelope.RequestId)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error routing message type: {MessageType}", envelope.Type);
            return CreateErrorResponse("INTERNAL_ERROR", 
                "An internal error occurred", 
                envelope.RequestId);
        }
    }
    
    private async Task<MessageEnvelope> HandleLoginAsync(MessageEnvelope envelope)
    {
        // TODO: Phase 5 - Implement login handler
        await Task.CompletedTask;
        return CreateErrorResponse("NOT_IMPLEMENTED", 
            "Login handler not yet implemented", 
            envelope.RequestId);
    }
    
    private async Task<MessageEnvelope> HandleUpdateResourcesAsync(MessageEnvelope envelope, string? currentPlayerId)
    {
        // TODO: Phase 6 - Implement update resources handler
        await Task.CompletedTask;
        return CreateErrorResponse("NOT_IMPLEMENTED", 
            "UpdateResources handler not yet implemented", 
            envelope.RequestId);
    }
    
    private async Task<MessageEnvelope> HandleSendGiftAsync(MessageEnvelope envelope, string? currentPlayerId)
    {
        // TODO: Phase 7 - Implement send gift handler
        await Task.CompletedTask;
        return CreateErrorResponse("NOT_IMPLEMENTED", 
            "SendGift handler not yet implemented", 
            envelope.RequestId);
    }
    
    private static MessageEnvelope CreateErrorResponse(string code, string message, string? requestId)
    {
        return new MessageEnvelope
        {
            Type = MessageType.Error,
            Payload = JsonSerializer.Serialize(new ErrorMessage
            {
                Code = code,
                Message = message,
                RequestId = requestId
            }),
            RequestId = requestId
        };
    }
}

