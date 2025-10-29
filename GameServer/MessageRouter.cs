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
    
    public MessageRouter(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _loginHandler = new LoginHandler(connectionManager);
    }
    
    public async Task<MessageEnvelope> RouteMessageAsync(MessageEnvelope envelope, string? currentPlayerId, WebSocket webSocket)
    {
        try
        {
            Log.Information("Routing message type: {MessageType}", envelope.Type);
            
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

