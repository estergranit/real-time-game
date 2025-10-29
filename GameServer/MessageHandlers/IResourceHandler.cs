using Shared;

namespace GameServer.MessageHandlers;

public interface IResourceHandler
{
    Task<UpdateResourcesResponse> HandleAsync(UpdateResourcesRequest request);
}

