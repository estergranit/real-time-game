using System.Text.Json;
using Serilog;
using Shared;

namespace GameClient;

public class CommandLineInterface
{
    private readonly WebSocketClient _client;
    private int _requestCounter = 0;
    
    public CommandLineInterface(WebSocketClient client)
    {
        _client = client;
        _client.MessageReceived += OnMessageReceived;
    }
    
    public async Task RunAsync()
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Real-Time Game Client");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  login <deviceId>              - Login with device ID");
        Console.WriteLine("  update <coins|rolls> <value>  - Update resources");
        Console.WriteLine("  gift <playerId> <coins|rolls> <value> - Send gift");
        Console.WriteLine("  balance                       - Show current balance");
        Console.WriteLine("  help                          - Show commands");
        Console.WriteLine("  exit                          - Disconnect and exit");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }
            
            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }
            
            var command = parts[0].ToLower();
            
            try
            {
                switch (command)
                {
                    case "login":
                        await HandleLoginAsync(parts);
                        break;
                    
                    case "update":
                        await HandleUpdateResourcesAsync(parts);
                        break;
                    
                    case "gift":
                        await HandleSendGiftAsync(parts);
                        break;
                    
                    case "balance":
                        HandleBalance();
                        break;
                    
                    case "help":
                        ShowHelp();
                        break;
                    
                    case "exit":
                        Console.WriteLine("Disconnecting...");
                        return;
                    
                    default:
                        Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Log.Error(ex, "Command execution failed");
            }
        }
    }
    
    private async Task HandleLoginAsync(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: login <deviceId>");
            return;
        }
        
        var deviceId = parts[1];
        
        var request = new LoginRequest { DeviceId = deviceId };
        var envelope = new MessageEnvelope
        {
            Type = MessageType.Login,
            Payload = JsonSerializer.Serialize(request),
            RequestId = GetNextRequestId()
        };
        
        var response = await _client.SendMessageAsync(envelope);
        
        if (response.Type == MessageType.Error)
        {
            var error = JsonSerializer.Deserialize<ErrorMessage>(response.Payload);
            Console.WriteLine($"❌ Login failed: {error?.Message}");
        }
        else if (response.Type == MessageType.LoginResponse)
        {
            var loginResponse = JsonSerializer.Deserialize<LoginResponse>(response.Payload);
            if (loginResponse?.Success == true)
            {
                Console.WriteLine($"✅ Login successful! PlayerId: {loginResponse.PlayerId}");
            }
            else
            {
                Console.WriteLine("❌ Login failed");
            }
        }
    }
    
    private async Task HandleUpdateResourcesAsync(string[] parts)
    {
        if (parts.Length < 3)
        {
            Console.WriteLine("Usage: update <coins|rolls> <value>");
            return;
        }
        
        if (!Enum.TryParse<ResourceType>(parts[1], true, out var resourceType))
        {
            Console.WriteLine("Invalid resource type. Use 'coins' or 'rolls'");
            return;
        }
        
        if (!int.TryParse(parts[2], out var value))
        {
            Console.WriteLine("Invalid value. Must be a positive integer");
            return;
        }
        
        var request = new UpdateResourcesRequest
        {
            PlayerId = _client.CurrentPlayerId ?? string.Empty,
            ResourceType = resourceType,
            ResourceValue = value
        };
        
        var envelope = new MessageEnvelope
        {
            Type = MessageType.UpdateResources,
            Payload = JsonSerializer.Serialize(request),
            RequestId = GetNextRequestId()
        };
        
        var response = await _client.SendMessageAsync(envelope);
        
        if (response.Type == MessageType.Error)
        {
            var error = JsonSerializer.Deserialize<ErrorMessage>(response.Payload);
            Console.WriteLine($"❌ Update failed: {error?.Message}");
        }
        else if (response.Type == MessageType.UpdateResourcesResponse)
        {
            var updateResponse = JsonSerializer.Deserialize<UpdateResourcesResponse>(response.Payload);
            if (updateResponse?.Success == true)
            {
                Console.WriteLine($"✅ Updated! {updateResponse.ResourceType}: {updateResponse.NewBalance}");
            }
            else
            {
                Console.WriteLine("❌ Update failed");
            }
        }
    }
    
    private async Task HandleSendGiftAsync(string[] parts)
    {
        if (parts.Length < 4)
        {
            Console.WriteLine("Usage: gift <playerId> <coins|rolls> <value>");
            return;
        }
        
        var friendPlayerId = parts[1];
        
        if (!Enum.TryParse<ResourceType>(parts[2], true, out var resourceType))
        {
            Console.WriteLine("Invalid resource type. Use 'coins' or 'rolls'");
            return;
        }
        
        if (!int.TryParse(parts[3], out var value))
        {
            Console.WriteLine("Invalid value. Must be a positive integer");
            return;
        }
        
        var request = new SendGiftRequest
        {
            SenderId = _client.CurrentPlayerId ?? string.Empty,
            FriendPlayerId = friendPlayerId,
            ResourceType = resourceType,
            ResourceValue = value
        };
        
        var envelope = new MessageEnvelope
        {
            Type = MessageType.SendGift,
            Payload = JsonSerializer.Serialize(request),
            RequestId = GetNextRequestId()
        };
        
        var response = await _client.SendMessageAsync(envelope);
        
        if (response.Type == MessageType.Error)
        {
            var error = JsonSerializer.Deserialize<ErrorMessage>(response.Payload);
            Console.WriteLine($"❌ Gift failed: {error?.Message}");
        }
        else if (response.Type == MessageType.SendGiftResponse)
        {
            var giftResponse = JsonSerializer.Deserialize<SendGiftResponse>(response.Payload);
            if (giftResponse?.Success == true)
            {
                Console.WriteLine($"✅ Gift sent! Your new balance: {giftResponse.SenderNewBalance}");
            }
            else
            {
                Console.WriteLine("❌ Gift failed");
            }
        }
    }
    
    private void HandleBalance()
    {
        if (_client.CurrentPlayerId == null)
        {
            Console.WriteLine("❌ Not logged in. Please login first.");
            return;
        }
        
        Console.WriteLine($"PlayerId: {_client.CurrentPlayerId}");
        Console.WriteLine("Note: Balance is tracked server-side. Use 'update' to add resources.");
    }
    
    private void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  login <deviceId>              - Login with device ID");
        Console.WriteLine("  update <coins|rolls> <value>  - Update resources (positive values only)");
        Console.WriteLine("  gift <playerId> <coins|rolls> <value> - Send gift to another player");
        Console.WriteLine("  balance                       - Show current player ID");
        Console.WriteLine("  help                          - Show this help message");
        Console.WriteLine("  exit                          - Disconnect and exit");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  login device123");
        Console.WriteLine("  update coins 100");
        Console.WriteLine("  gift player_2 coins 50");
        Console.WriteLine();
    }
    
    private void OnMessageReceived(object? sender, MessageEnvelope envelope)
    {
        // Handle unsolicited messages like GiftEvent
        if (envelope.Type == MessageType.GiftEvent)
        {
            var giftEvent = JsonSerializer.Deserialize<GiftEvent>(envelope.Payload);
            if (giftEvent != null)
            {
                Console.WriteLine();
                Console.WriteLine($"🎁 Gift received from {giftEvent.FromPlayerId}!");
                Console.WriteLine($"   +{giftEvent.ResourceValue} {giftEvent.ResourceType}");
                Console.WriteLine($"   New balance: {giftEvent.NewBalance}");
                Console.Write("> ");
            }
        }
    }
    
    private string GetNextRequestId()
    {
        return $"req_{Interlocked.Increment(ref _requestCounter)}";
    }
}




