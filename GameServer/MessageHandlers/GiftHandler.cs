using Serilog;
using Shared;
using System.Text.Json;

namespace GameServer.MessageHandlers;

public class GiftHandler : IGiftHandler
{
    private readonly ConnectionManager _connectionManager;
    
    public GiftHandler(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
    
    public async Task<SendGiftResponse> HandleAsync(SendGiftRequest request)
    {
        await Task.CompletedTask; // Ensure async signature
        
        // Validate gift value is positive
        if (request.ResourceValue <= 0)
        {
            Log.Warning("SendGift rejected - non-positive value: {Value}", request.ResourceValue);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = 0
            };
        }
        
        // Find sender
        var sender = _connectionManager.GetPlayerByPlayerId(request.SenderId);
        if (sender == null)
        {
            Log.Warning("SendGift failed - sender not found: {SenderId}", request.SenderId);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = 0
            };
        }
        
        // Find recipient
        var recipient = _connectionManager.GetPlayerByPlayerId(request.FriendPlayerId);
        if (recipient == null)
        {
            Log.Warning("SendGift failed - recipient not found: {RecipientId}", request.FriendPlayerId);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = sender.GetBalance(request.ResourceType)
            };
        }
        
        // Prevent self-gifting
        if (request.SenderId == request.FriendPlayerId)
        {
            Log.Warning("SendGift rejected - cannot gift to self: {PlayerId}", request.SenderId);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = sender.GetBalance(request.ResourceType)
            };
        }
        
        // Lock both players to prevent race conditions
        // Lock ordering: always lock by string comparison to prevent deadlocks
        var firstLock = string.Compare(sender.PlayerId, recipient.PlayerId, StringComparison.Ordinal) < 0 
            ? sender 
            : recipient;
        var secondLock = firstLock == sender ? recipient : sender;
        
        lock (firstLock)
        {
            lock (secondLock)
            {
                // Check sender has sufficient balance
                var senderBalance = sender.GetBalance(request.ResourceType);
                if (senderBalance < request.ResourceValue)
                {
                    Log.Warning("SendGift rejected - insufficient balance. {SenderId} has {Balance}, needs {Amount}",
                        request.SenderId, senderBalance, request.ResourceValue);
                    return new SendGiftResponse
                    {
                        Success = false,
                        SenderNewBalance = senderBalance
                    };
                }
                
                // Deduct from sender
                var senderSuccess = sender.TryUpdateBalance(request.ResourceType, -request.ResourceValue, out var senderNewBalance);
                if (!senderSuccess)
                {
                    Log.Error("SendGift failed - sender balance update failed unexpectedly");
                    return new SendGiftResponse
                    {
                        Success = false,
                        SenderNewBalance = senderBalance
                    };
                }
                
                // Add to recipient
                var recipientSuccess = recipient.TryUpdateBalance(request.ResourceType, request.ResourceValue, out var recipientNewBalance);
                if (!recipientSuccess)
                {
                    // This should never happen since we're adding, but roll back sender if it does
                    sender.TryUpdateBalance(request.ResourceType, request.ResourceValue, out _);
                    Log.Error("SendGift failed - recipient balance update failed unexpectedly");
                    return new SendGiftResponse
                    {
                        Success = false,
                        SenderNewBalance = senderBalance
                    };
                }
                
                Log.Information("SendGift success - {SenderId} sent {Amount} {ResourceType} to {RecipientId}. Sender balance: {SenderBalance}, Recipient balance: {RecipientBalance}",
                    request.SenderId, request.ResourceValue, request.ResourceType, request.FriendPlayerId, senderNewBalance, recipientNewBalance);
                
                // Send GiftEvent to recipient if they're online
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var giftEvent = new GiftEvent
                        {
                            FromPlayerId = request.SenderId,
                            ResourceType = request.ResourceType,
                            ResourceValue = request.ResourceValue,
                            NewBalance = recipientNewBalance
                        };
                        
                        var envelope = new MessageEnvelope
                        {
                            Type = MessageType.GiftEvent,
                            Payload = JsonSerializer.Serialize(giftEvent)
                        };
                        
                        await _connectionManager.SendMessageAsync(recipient, envelope);
                        Log.Information("GiftEvent sent to online recipient: {RecipientId}", request.FriendPlayerId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to send GiftEvent to recipient: {RecipientId}", request.FriendPlayerId);
                    }
                });
                
                return new SendGiftResponse
                {
                    Success = true,
                    SenderNewBalance = senderNewBalance
                };
            }
        }
    }
}

