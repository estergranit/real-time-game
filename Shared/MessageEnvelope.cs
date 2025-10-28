using System.Text.Json.Serialization;

namespace Shared;

public class MessageEnvelope
{
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }
    
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
    
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }
}

