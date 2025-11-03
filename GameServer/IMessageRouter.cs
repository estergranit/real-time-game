using System.Net.WebSockets;
using Shared;

namespace GameServer;

public interface IMessageRouter
{
    Task<MessageEnvelope> RouteMessageAsync(MessageEnvelope envelope, string? currentPlayerId, WebSocket webSocket);
}

