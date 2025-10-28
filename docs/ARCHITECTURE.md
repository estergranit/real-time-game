# Architecture Overview

## System Design

### Technology Stack
- **.NET 8**: Target framework
- **ASP.NET Core**: Minimal API hosting with Kestrel
- **System.Net.WebSockets**: WebSocket communication (no SignalR)
- **Serilog**: Structured logging (Console + File sinks)
- **In-memory storage**: Player state (SQLite optional future enhancement)

### Communication Protocol
- **Transport**: WebSocket (JSON messages)
- **Endpoint**: `/ws` on port 8080
- **Message Format**: JSON-serialized envelopes with type routing

### Message Types

#### 1. Login
- **Input**: DeviceId (UDID)
- **Output**: PlayerId
- **Rules**: Reject duplicate connections with same DeviceId

#### 2. UpdateResources
- **Input**: ResourceType (coins | rolls), ResourceValue (positive integer)
- **Output**: Updated balance
- **Validation**: Only positive increments allowed, reject if balance would go negative

#### 3. SendGift
- **Input**: FriendPlayerId, ResourceType, ResourceValue
- **Actions**:
  - Validate sender has sufficient balance
  - Deduct from sender atomically
  - Add to recipient atomically
  - Send GiftEvent notification if recipient is online
- **Error**: Reject if sender has insufficient balance

### Concurrency Model

- **ConnectionManager**: `ConcurrentDictionary` for thread-safe connection tracking
- **PlayerState**: Lock-based synchronization per player for atomic updates
- **Gift Transfers**: Ordered locking (by PlayerId) to prevent deadlocks

### Design Principles

- **SOLID**: Clean separation of concerns via interfaces
- **Async/Await**: All I/O operations are asynchronous
- **Race-condition safety**: Proper locking for shared state
- **Extensibility**: Easy to add new message handlers

## Future Enhancements
- SQLite persistence for player state
- Redis for distributed state management
- Horizontal scaling with load balancing
- Metrics and performance monitoring
- Authentication and authorization

