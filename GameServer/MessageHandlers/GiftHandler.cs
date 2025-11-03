using Serilog;
using Shared;
using System.Text.Json;
using System.Threading;

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
            var currentBalance = await sender.GetBalanceAsync(request.ResourceType);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = currentBalance
            };
        }
        
        // Prevent self-gifting
        if (request.SenderId == request.FriendPlayerId)
        {
            Log.Warning("SendGift rejected - cannot gift to self: {PlayerId}", request.SenderId);
            var currentBalance = await sender.GetBalanceAsync(request.ResourceType);
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = currentBalance
            };
        }
        
        // Check sender has sufficient balance
        var senderBalance = await sender.GetBalanceAsync(request.ResourceType);
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
        var senderResult = await sender.TryUpdateBalanceAsync(request.ResourceType, -request.ResourceValue);
        if (!senderResult.Success)
        {
            Log.Error("SendGift failed - sender balance update failed unexpectedly");
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = senderBalance
            };
        }

        // Add to recipient
        var recipientResult = await recipient.TryUpdateBalanceAsync(request.ResourceType, request.ResourceValue);
        if (!recipientResult.Success)
        {
            // This should never happen since we're adding, but roll back sender if it does
            await sender.TryUpdateBalanceAsync(request.ResourceType, request.ResourceValue);
            Log.Error("SendGift failed - recipient balance update failed unexpectedly");
            return new SendGiftResponse
            {
                Success = false,
                SenderNewBalance = senderBalance
            };
        }

        Log.Information("SendGift success - {SenderId} sent {Amount} {ResourceType} to {RecipientId}. Sender balance: {SenderBalance}, Recipient balance: {RecipientBalance}",
            request.SenderId, request.ResourceValue, request.ResourceType, request.FriendPlayerId, senderResult.NewBalance, recipientResult.NewBalance);

        // Send GiftEvent to recipient if they're online (leave fire-and-forget change to later fix)
        _ = Task.Run(async () =>
        {
            try
            {
                var giftEvent = new GiftEvent
                {
                    FromPlayerId = request.SenderId,
                    ResourceType = request.ResourceType,
                    ResourceValue = request.ResourceValue,
                    NewBalance = recipientResult.NewBalance
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
            SenderNewBalance = senderResult.NewBalance
        };
    }
}

