using Shared;

namespace GameServer.MessageHandlers;

public interface IGiftHandler
{
    Task<SendGiftResponse> HandleAsync(SendGiftRequest request);
}

