# Real-Time Game Server

.NET 8 WebSocket-based game server with resource management and gifting system.

## Projects

- **GameServer**: ASP.NET Core WebSocket server (Kestrel on port 8080)
- **GameClient**: Console client for manual testing
- **Shared**: Shared message contracts and DTOs

## Requirements

- .NET 8 SDK
- Windows/Linux/macOS

## Quick Start

### Run Server
```bash
dotnet run --project GameServer
```
Server listens on http://localhost:8080
WebSocket endpoint: ws://localhost:8080/ws

### Run Client
```bash
dotnet run --project GameClient
```

## Features

- **Login**: Authenticate with DeviceId, receive PlayerId
- **UpdateResources**: Add coins/rolls with balance validation
- **SendGift**: Transfer resources between players with real-time notifications

## Architecture

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed design documentation.

## Development Status

✅ Phase 1: Solution scaffolding (Current)
⏳ Phase 2: Shared contracts
⏳ Phase 3: Server infrastructure
⏳ Phase 4-10: Implementation phases

## Logging

Both server and client use Serilog:
- Console output for development
- File logs in `logs/` directory (rolling daily)

