# Pending Fixes

Items from architecture review not yet implemented.

---

## ⚠️ Important

- **CancellationToken propagation**: Add graceful shutdown through handlers  
- **Task.Run notification**: Replace fire-and-forget in GiftHandler  
- **RequestId correlation**: Add to all logs for tracing  
- **JSON parse errors**: Send error responses to client  
- **Structured exceptions**: Specific catch blocks instead of `catch (Exception)`
- **Resource balance consistency**:  
  During concurrent `SendGift` operations, sender and recipient balances may temporarily desynchronize due to race conditions between balance updates and event dispatch.  
  **Fix:** Ensure atomic balance updates and commit before triggering `GiftEvent`.
- **GiftEvent payload accuracy**:  
  `GiftEvent` occasionally reports outdated recipient balance when emitted before persistence completes.  
  **Fix:** Defer event emission until after updated balances are confirmed.

---

## 🧹 Polish

- **Double-lock optimization**: Avoid redundant semaphore in GiftHandler
- **Log verbosity**: Change routing logs to Debug level
- **Configuration**: Extract magic numbers to constants
- **Input validation**: Add parameter checks in ConnectionManager
- **Request mutation**: Don't mutate DTOs in router
- **JsonSerializerOptions**: Reuse static instance
- **Error codes**: Replace strings with enum
- **Connection cleanup**: Implement IDisposable

