# Architecture Overview

Current implementation of the Real-Time Game Server.

---

## 🏗️ Stack

**Core:**
- .NET 8, ASP.NET Core Minimal API
- Raw WebSockets (no SignalR)
- Serilog for logging
- In-memory storage

**Architecture:** SOLID, DI, interface-based

---

## 🧩 Components

### PlayerState
Thread-safe resource storage with `SemaphoreSlim`:

```csharp
private readonly SemaphoreSlim _resourceSemaphore = new(1, 1);

public async Task<(bool, int)> TryUpdateBalanceAsync(ResourceType type, int delta)
{
    await _semaphore.WaitAsync();
    try
    {
        var balance = _coins + delta;
        if (balance < 0) return (false, _coins);
        _coins = balance;
        return (true, balance);
    }
    finally { _semaphore.Release(); }
}
```

### ConnectionManager
Dual `ConcurrentDictionary` lookups:
- `_playersByDeviceId` → prevent duplicate logins
- `_playersByPlayerId` → route gifts

### MessageRouter
Type-safe routing:
```csharp
return envelope.Type switch
{
    MessageType.Login => await HandleLoginAsync(envelope, webSocket),
    MessageType.UpdateResources => await HandleUpdateResourcesAsync(envelope, playerId),
    MessageType.SendGift => await HandleSendGiftAsync(envelope, playerId),
    _ => CreateErrorResponse(...)
};
```

### WebSocketHandler
Manages connection lifecycle, JSON serialization, message loop.

---

## 🔒 Concurrency

**Problem:** Race conditions, deadlocks, data loss

**Solutions:**
1. **SemaphoreSlim** over `lock` (no thread blocking)
2. **Ordered locking** by PlayerId to avoid circular waits
3. **Timeouts** on lock acquisition (prevent hangs)

**Gift transfer locking:**
```csharp
var first = sender.PlayerId.CompareTo(recipient.PlayerId) < 0 ? sender : recipient;
var second = first == sender ? recipient : sender;

await first.ResourceSemaphore.WaitAsync();
try
{
    await second.ResourceSemaphore.WaitAsync();
    try { /* transfer */ }
    finally { second.ResourceSemaphore.Release(); }
}
finally { first.ResourceSemaphore.Release(); }
```

---

## 📨 Message Flow

**Gift example:**
1. Client → `SendGift` envelope
2. Router → GiftHandler
3. Handler: validate → lock ordered → atomic transfer
4. If online, notify via ConnectionManager
5. Response envelope → client

**All messages:** Envelope pattern with type routing and RequestId correlation.

---

## 🧪 Testing

Stress tests in `Test/`:
- 10 concurrent gifts (balance accuracy)
- 500+ ops per player (no deadlocks)
- 50 login races (only 1 succeeds)

---

## 🏛️ Principles

**Async-first:** All I/O is async/await  
**SOLID:** Clean separation, interfaces  
**Extensible:** Add handlers without changing router  
**Thread-safe:** Atomic operations with timeouts

---

**Current state:** Demo-ready, in-memory, single instance  
**Production gaps:** Persistence, horizontal scaling, auth, metrics

