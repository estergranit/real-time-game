using System.Net.WebSockets;
using System.Text.Json;
using GameServer.MessageHandlers;
using Serilog;
using Shared;

namespace GameServer;

public class MessageRouter
{
    private readonly ConnectionManager _connectionManager;
    private readonly LoginHandler _loginHandler;
    private readonly ResourceHandler _resourceHandler;
    
    public MessageRouter(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _loginHandler = new LoginHandler(connectionManager);
        _resourceHandler = new ResourceHandler(connectionManager);
    }
    
    public async Task<MessageEnvelope> RouteMessageAsync(MessageEnvelope envelope, string? currentPlayerId, WebSocket webSocket)
    {
        try
        {
            Log.Information("Routing message type: {MessageType}, CurrentPlayerId: {CurrentPlayerId}", 
                envelope.Type, currentPlayerId ?? "(not authenticated)");
            
            return envelope.Type switch
            {
                MessageType.Login => await HandleLoginAsync(envelope, webSocket),
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
    
    private async Task<MessageEnvelope> HandleLoginAsync(MessageEnvelope envelope, WebSocket webSocket)
    {
        try
        {
            var request = JsonSerializer.Deserialize<LoginRequest>(envelope.Payload);
            if (request == null)
            {
                return CreateErrorResponse("INVALID_REQUEST", 
                    "Invalid login request format", 
                    envelope.RequestId);
            }
            
            var response = await _loginHandler.HandleAsync(request, webSocket);
            
            if (!response.Success)
            {
                return CreateErrorResponse("LOGIN_FAILED", 
                    "Login failed - device already connected or invalid request", 
                    envelope.RequestId);
            }
            
            return new MessageEnvelope
            {
                Type = MessageType.LoginResponse,
                Payload = JsonSerializer.Serialize(response),
                RequestId = envelope.RequestId
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling login");
            return CreateErrorResponse("LOGIN_ERROR", 
                "Error processing login request", 
                envelope.RequestId);
        }
    }
    
    private async Task<MessageEnvelope> HandleUpdateResourcesAsync(MessageEnvelope envelope, string? currentPlayerId)
    {
        // Require authentication
        if (string.IsNullOrEmpty(currentPlayerId))
        {
            Log.Warning("UpdateResources rejected - player not authenticated");
            return CreateErrorResponse("NOT_AUTHENTICATED", 
                "You must login first", 
                envelope.RequestId);
        }
        
        Log.Information("UpdateResources called by authenticated player: {PlayerId}", currentPlayerId);
        
        try
        {
            var request = JsonSerializer.Deserialize<UpdateResourcesRequest>(envelope.Payload);
            if (request == null)
            {
                return CreateErrorResponse("INVALID_REQUEST", 
                    "Invalid update resources request format", 
                    envelope.RequestId);
            }
            
            // Ensure player is updating their own resources
            request.PlayerId = currentPlayerId;
            
            var response = await _resourceHandler.HandleAsync(request);
            
            if (!response.Success)
            {
                return CreateErrorResponse("UPDATE_FAILED", 
                    "Resource update failed - invalid value or would result in negative balance", 
                    envelope.RequestId);
            }
            
            return new MessageEnvelope
            {
                Type = MessageType.UpdateResourcesResponse,
                Payload = JsonSerializer.Serialize(response),
                RequestId = envelope.RequestId
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling update resources");
            return CreateErrorResponse("UPDATE_ERROR", 
                "Error processing update resources request", 
                envelope.RequestId);
        }
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

