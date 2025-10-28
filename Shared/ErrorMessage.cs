using System.Text.Json.Serialization;

namespace Shared;

public class ErrorMessage
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }
}

