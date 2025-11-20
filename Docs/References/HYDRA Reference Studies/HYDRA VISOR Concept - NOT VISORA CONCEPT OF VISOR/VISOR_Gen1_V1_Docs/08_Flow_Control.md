# 08 - Flow Control & Credit System

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

Flow control in VISOR uses a credit-based system via VisorConnectionQueue to prevent overwhelming clients.

---

## 8.1 VisorConnectionQueue

**File:** `VisorConnectionQueue.cs` (158 lines)
**Status:** ✅ Operational

### Architecture

```
┌─────────────────────────────────────┐
│   VisorConnectionQueue              │
├─────────────────────────────────────┤
│                                      │
│  ConcurrentQueue<HydraEnvelope>     │
│  - Max size: 1000 messages          │
│  - Ring buffer behavior              │
│                                      │
│  Credit System                       │
│  - Initial: 10 messages              │
│  - Consumed per dequeue              │
│  - Replenished by client             │
│                                      │
└─────────────────────────────────────┘
```

### Properties

```csharp
public sealed class VisorConnectionQueue
{
    private readonly ConcurrentQueue<HydraEnvelope> _queue = new();
    private readonly object _creditLock = new();
    private int _credit;
    private readonly int _maxQueueSize;

    public VisorConnectionQueue(int initialCredit = 10, int maxQueueSize = 1000)
    {
        _credit = initialCredit;
        _maxQueueSize = maxQueueSize;
    }

    public int Credit { get; } // Getter with lock
    public int Count => _queue.Count;
}
```

**Source:** Lines 21-56

---

## 8.2 Credit Flow

### Initial State

```
Connection established → Credit = 10
Queue empty
```

### Enqueue (Lines 63-72)

```csharp
public bool TryEnqueue(HydraEnvelope envelope)
{
    if (_queue.Count >= _maxQueueSize)
    {
        return false;  // Queue full, reject
    }

    _queue.Enqueue(envelope);
    return true;
}
```

**No credit check on enqueue** - only size limit

### Dequeue (Lines 80-99)

```csharp
public bool TryDequeue(out HydraEnvelope? envelope)
{
    lock (_creditLock)
    {
        if (_credit <= 0 || _queue.IsEmpty)
        {
            envelope = null;
            return false;  // No credit or empty queue
        }

        if (_queue.TryDequeue(out envelope))
        {
            _credit--;  // Consume credit
            return true;
        }

        envelope = null;
        return false;
    }
}
```

**Credit consumed per successful dequeue**

### Credit Replenishment (Lines 105-114)

```csharp
public void AddCredit(int amount)
{
    if (amount <= 0)
        throw new ArgumentException("Credit amount must be positive");

    lock (_creditLock)
    {
        _credit += amount;
    }
}
```

**Triggered by client messages** (VisorGateway.cs:289-293):
```csharp
if (envelope.Flow?.Credit != null)
{
    queue.AddCredit(envelope.Flow.Credit.Value);
}
```

---

## 8.3 Backpressure Strategy

### When Credit Runs Out

```
Credit = 0 → TryDequeue() fails → SendLoop pauses → No messages sent
```

### Recovery

```
Client sends envelope with Flow.Credit → AddCredit() called → Credit restored → SendLoop resumes
```

### Example Flow

```
1. Connection starts: Credit = 10
2. Send 10 messages: Credit = 0
3. Client ACKs with Flow.Credit = 5
4. Credit = 5
5. Send 5 more messages: Credit = 0
6. Wait for client ACK...
```

---

## 8.4 Credit Unit (MVP)

**Current:** Messages (not bytes)

```csharp
// Each message = 1 credit
_credit--;  // Dequeue consumes 1 credit
```

**Future:** Configurable units
- Bytes (for large message handling)
- Weighted by priority
- Dynamic based on client capacity

---

## 8.5 SendLoop Integration

**VisorGateway SendLoop** (Lines 318-346):

```csharp
private async Task SendLoop(
    string connectionId,
    WebSocket webSocket,
    VisorConnectionQueue queue,
    CancellationToken cancellationToken)
{
    while (webSocket.State == WebSocketState.Open)
    {
        // Check regular envelopes
        if (queue.TryDequeue(out var envelope) && envelope != null)
        {
            await SendEnvelope(webSocket, envelope, cancellationToken);
        }

        // Check pulse scheduler
        var duePulses = _pulseScheduler.GetDuePulses(connectionId, queue.Credit);
        foreach (var pulse in duePulses)
        {
            await SendEnvelope(webSocket, pulse, cancellationToken);
        }

        await Task.Delay(10, cancellationToken); // Brief pause
    }
}
```

**Key:** `queue.Credit` passed to `GetDuePulses()` as max count

---

## 8.6 Connection Lifecycle

### Creation (VisorGateway.cs:174)

```csharp
var queue = new VisorConnectionQueue();  // Credit = 10, MaxSize = 1000
_connectionQueues[connectionId] = queue;
```

### Cleanup (VisorGateway.cs:223)

```csharp
_connectionQueues.TryRemove(connectionId, out _);
```

---

## 8.7 Alternative Operations

### ResetCredit (Lines 120-126)

```csharp
public void ResetCredit(int amount)
{
    lock (_creditLock)
    {
        _credit = amount;
    }
}
```

**Use case:** Reconnection, credit reset

### DrainAll (Lines 132-142)

```csharp
public List<HydraEnvelope> DrainAll()
{
    var result = new List<HydraEnvelope>();

    while (_queue.TryDequeue(out var envelope))
    {
        result.Add(envelope);
    }

    return result;
}
```

**Use case:** Emergency drain on disconnect

### Clear (Lines 148-156)

```csharp
public void Clear(int newCredit = 10)
{
    while (_queue.TryDequeue(out _)) { }

    lock (_creditLock)
    {
        _credit = newCredit;
    }
}
```

**Use case:** Reset connection state

---

## 8.8 Known Limitations

### Fixed Initial Credit
- Always 10 (not configurable per connection)
- No negotiation with client
- No dynamic adjustment based on client capacity

### Credit Replenishment
- Requires client cooperation
- Client must send `Flow.Credit` in envelope
- No automatic refresh mechanism
- No timeout if client never sends credit

### Max Queue Size
- Fixed at 1000 (not configurable)
- No overflow handling (just rejects)
- No priority-based eviction

### No Fairness
- First-come-first-served within connection
- No priority weighting for credit consumption
- No burst credit for high-priority messages

---

## 8.9 Future Enhancements

**Dynamic Credit:**
- Adjust based on client processing rate
- Monitor latency and adjust credit
- Burst credit for priority messages

**Byte-based Credit:**
- Track bytes instead of message count
- Large messages consume more credit
- More accurate bandwidth control

**Credit Negotiation:**
- Handshake to agree on initial credit
- Client advertises capacity
- Server adjusts accordingly

**Fairness:**
- Priority-weighted credit consumption
- Fair queuing across connections
- Prevent one connection starving others

---

**See Also:**
- [05 - VisorGateway](./05_VisorGateway.md) - SendLoop integration
- [07 - Batching & Pulsing](./07_Batching_Pulsing.md) - PulseScheduler credit usage
- [11 - Evolution Roadmap](./11_Evolution_Roadmap.md) - Future plans
