using Shared;

namespace GameServer.MessageHandlers;

public interface ILoginHandler
{
    Task<LoginResponse> HandleAsync(LoginRequest request, System.Net.WebSockets.WebSocket webSocket);
}

