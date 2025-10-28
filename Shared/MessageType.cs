namespace Shared;

public enum MessageType
{
    // Requests
    Login,
    UpdateResources,
    SendGift,
    
    // Responses (success)
    LoginResponse,
    UpdateResourcesResponse,
    SendGiftResponse,
    
    // Events
    GiftEvent,
    
    // Error
    Error
}

