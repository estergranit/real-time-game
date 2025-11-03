# Real-Time Game Server

A high-performance .NET WebSocket-based game server demonstrating advanced concurrency patterns, thread-safety, and clean architecture principles.

---

## 🎯 Project Overview

This is a **real-time game server** built with .NET 9 that manages player authentication, resource updates, and gift transfers over WebSocket connections. The system is designed to handle **high concurrency** with **zero data loss** and **no race conditions**, making it production-ready for real-time multiplayer games.

**Key Components:**
- **GameServer**: ASP.NET Core WebSocket server with message routing and handler infrastructure
- **Shared**: Common message contracts and DTOs shared across projects
- **GameClient**: Console-based testing client with interactive CLI
- **Test**: Concurrency stress tests and race condition verification

---

## 🧱 Architecture Overview

### System Design

The server follows a **clean, layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────┐
│                    GameClient                       │
│         (Interactive Testing Console)               │
└────────────────────┬────────────────────────────────┘
                     │ WebSocket (JSON)
                     ▼
┌─────────────────────────────────────────────────────┐
│                  GameServer                         │
│  ┌──────────────────────────────────────────────┐  │
│  │         WebSocketHandler                     │  │
│  │         (Connection lifecycle)               │  │
│  └────────────────┬─────────────────────────────┘  │
│                   │                                 │
│  ┌────────────────▼─────────────────────────────┐  │
│  │         MessageRouter                        │  │
│  │         (Type-based routing)                 │  │
│  └────┬────────────┬────────────┬──────────────┘  │
│       │            │            │                  │
│  ┌────▼────┐ ┌─────▼────┐ ┌────▼────┐           │
│  │ Login   │ │Resource  │ │  Gift   │           │
│  │ Handler │ │ Handler  │ │ Handler │           │
│  └─────────┘ └──────────┘ └─────────┘           │
│       │            │            │                  │
│  ┌────▼──────────────────────────▼──────────────┐  │
│  │      ConnectionManager                       │  │
│  │      (Player state & connections)            │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  ┌──────────────────────────────────────────────┐  │
│  │      PlayerState                             │  │
│  │      (Thread-safe resource management)       │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Technology Stack

- **Framework**: .NET 9 (C#)
- **Web Framework**: ASP.NET Core with Kestrel
- **Communication**: System.Net.WebSockets (raw WebSockets, no SignalR)
- **Logging**: Serilog with Console and File sinks
- **Storage**: In-memory (thread-safe collections)
- **Architecture**: SOLID principles, Dependency Injection, Interface-based design

### Communication Protocol

- **Endpoint**: `ws://localhost:8080/ws`
- **Message Format**: JSON-encoded envelopes with type routing
- **Message Types**: Login, UpdateResources, SendGift, GiftEvent, Error

---

## ✨ Core Features

### 1. Login & Authentication
- Device-based authentication with unique PlayerId generation
- **Duplicate connection prevention**: Rejects multiple simultaneous connections from the same device
- **Race-condition safe**: Uses atomic lock-and-check operations

### 2. Resource Management
- Thread-safe balance updates for coins and rolls
- **Validation**: Rejects negative values and prevents negative balances
- **Atomic operations**: Each update is fully isolated and consistent

### 3. Gift Transfer System
- Transfer resources between players with real-time notifications
- **Concurrency-safe**: Ordered locking prevents deadlocks
- **Balance validation**: Ensures sender has sufficient funds before transfer
- **Online notifications**: Sends GiftEvent to recipient if connected

### 4. Advanced Concurrency
- **SemaphoreSlim synchronization**: Proper async-aware locks (no blocking)
- **Ordered locking**: Prevents deadlocks in gift transfers
- **Lock timeout protection**: Prevents indefinite hangs

---

## ⚙️ Technical Highlights

### Async-Aware Synchronization
The system uses `SemaphoreSlim` instead of `lock` statements to prevent thread pool starvation:

```csharp
private readonly SemaphoreSlim _resourceSemaphore = new(1, 1);

public async Task<int> GetBalanceAsync(ResourceType resourceType)
{
    await _resourceSemaphore.WaitAsync();
    try { /* ... */ }
    finally { _resourceSemaphore.Release(); }
}
```

### Dependency Injection Architecture
Clean separation with interface-based design:

```csharp
builder.Services.AddScoped<ILoginHandler, LoginHandler>();
builder.Services.AddScoped<IResourceHandler, ResourceHandler>();
builder.Services.AddScoped<IGiftHandler, GiftHandler>();
builder.Services.AddScoped<IMessageRouter, MessageRouter>();
```

### Structured Logging
Comprehensive logging with Serilog for debugging and monitoring:

- **Information**: State changes (login, disconnect, transfers)
- **Warning**: Validation failures, race conditions
- **Error**: Exceptions and system failures

### Message Routing Pattern
Type-safe envelope-based message routing:

```csharp
public async Task<MessageEnvelope> RouteMessageAsync(
    MessageEnvelope envelope, 
    string? currentPlayerId, 
    WebSocket webSocket)
{
    return envelope.Type switch
    {
        MessageType.Login => await HandleLoginAsync(envelope, webSocket),
        MessageType.UpdateResources => await HandleUpdateResourcesAsync(envelope, currentPlayerId),
        MessageType.SendGift => await HandleSendGiftAsync(envelope, currentPlayerId),
        _ => CreateErrorResponse("UNKNOWN_MESSAGE_TYPE", ...)
    };
}
```

---

## 🧩 Project Structure

```
real-time-game/
├── GameServer/                 # ASP.NET Core WebSocket server
│   ├── MessageHandlers/       # Business logic handlers
│   │   ├── LoginHandler.cs
│   │   ├── ResourceHandler.cs
│   │   └── GiftHandler.cs
│   ├── ConnectionManager.cs   # Player connection management
│   ├── PlayerState.cs         # Thread-safe player state
│   ├── MessageRouter.cs       # Message routing
│   ├── WebSocketHandler.cs    # Connection lifecycle
│   └── Program.cs             # Startup & DI configuration
│
├── Shared/                    # Common contracts
│   ├── MessageType.cs         # Message type enum
│   ├── MessageEnvelope.cs     # Envelope wrapper
│   ├── LoginMessage.cs        # Login request/response
│   ├── ResourceMessage.cs     # Resource update messages
│   └── GiftMessage.cs         # Gift transfer messages
│
├── GameClient/                # Testing console client
│   ├── WebSocketClient.cs     # WebSocket wrapper
│   ├── CommandLineInterface.cs # Interactive CLI
│   └── Program.cs             # Entry point
│
├── Test/                      # Concurrency tests
│   └── Program.cs             # Test runner
│
└── docs/                      # Documentation
    ├── ARCHITECTURE.md        # System design details
    └── PENDING_FIXES.md       # Known improvements
    
```

---

## 🚀 Running the Project

### Prerequisites

- **.NET 8 SDK** or later
- Windows, Linux, or macOS

### Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd real-time-game
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the solution**
   ```bash
   dotnet build
   ```

4. **Start the server**
   ```bash
   dotnet run --project GameServer
   ```
   Server will listen on `http://localhost:8080`

5. **Run the client** (in a separate terminal)
   ```bash
   dotnet run --project GameClient
   ```

### Server Commands

Once the client is running, you can use these commands:

```
login <deviceId>           # Authenticate with device ID
update <type> <value>      # Update resources (coins/rolls)
gift <playerId> <type> <value>  # Send gift to another player
balance                    # Show current balance
help                       # Display available commands
exit                       # Disconnect and quit
```

**Example Session:**
```
> login device_123
✅ Login successful! PlayerId: player_1

> update coins 1000
✅ Update successful! New balance: 1000

> gift player_2 coins 500
✅ Gift sent successfully! Your balance: 500
🎁 Gift received from player_1!
   +500 coins
   New balance: 500
```

---

## 🧪 Testing

The project includes **stress tests** to verify concurrency safety and race condition prevention.

### Run Concurrency Tests

**Gift Transfer Stress Test** (10 concurrent gifts, verifies balance accuracy):
```bash
dotnet run --project Test
```

**WebSocket Lock Stress Test** (500+ concurrent operations per player):
```bash
dotnet run --project Test -- websocket
```

**Login Race Condition Test** (50 concurrent login attempts):
```bash
dotnet run --project Test -- login
```

### Test Results

All tests verify:
- ✅ **No data loss** - All operations complete atomically
- ✅ **No race conditions** - Only one operation succeeds when competing
- ✅ **No deadlocks** - Lock timeouts prevent indefinite hangs
- ✅ **Correct balances** - Final state matches expected values

---

## 📖 Additional Documentation

- **[ARCHITECTURE.md](docs/ARCHITECTURE.md)**: Detailed system design and design decisions
- **[PENDING_FIXES.md](docs/PENDING_FIXES.md)**: Comprehensive list of planned improvements



