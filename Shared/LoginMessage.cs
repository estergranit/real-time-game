using System.Text.Json.Serialization;

namespace Shared;

public class LoginRequest
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;
}

public class LoginResponse
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = string.Empty;
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

