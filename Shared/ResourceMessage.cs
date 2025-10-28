using System.Text.Json.Serialization;

namespace Shared;

public enum ResourceType
{
    Coins,
    Rolls
}

public class UpdateResourcesRequest
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;
    
    [JsonPropertyName("resourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceType ResourceType { get; set; }
    
    [JsonPropertyName("resourceValue")]
    public int ResourceValue { get; set; }
}

public class UpdateResourcesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("resourceType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResourceType ResourceType { get; set; }
    
    [JsonPropertyName("newBalance")]
    public int NewBalance { get; set; }
}

