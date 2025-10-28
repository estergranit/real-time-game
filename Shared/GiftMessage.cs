using System.Text.Json.Serialization;

namespace Shared;

public class SendGiftRequest
{
    [JsonPropertyName("senderId")]
    public string SenderId { get; set; } = string.Empty;
    
    [JsonPropertyName("friendPlayerId")]
    public string FriendPlayerId { get; set; } = string.Empty;
    
    [JsonPropertyName("resourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceType ResourceType { get; set; }
    
    [JsonPropertyName("resourceValue")]
    public int ResourceValue { get; set; }
}

public class SendGiftResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("senderNewBalance")]
    public int SenderNewBalance { get; set; }
}

public class GiftEvent
{
    [JsonPropertyName("fromPlayerId")]
    public string FromPlayerId { get; set; } = string.Empty;
    
    [JsonPropertyName("resourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceType ResourceType { get; set; }
    
    [JsonPropertyName("resourceValue")]
    public int ResourceValue { get; set; }
    
    [JsonPropertyName("newBalance")]
    public int NewBalance { get; set; }
}

