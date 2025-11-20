# Flow Control & Batching Pattern - Credit-Based Backpressure

**Pattern Category:** Concurrency / Performance
**Complexity:** Medium-High
**Reusability:** High - applicable to any producer-consumer system with flow control

---

## Pattern Intent

Prevent message flooding in bidirectional systems with:
- Credit-based backpressure (receiver controls send rate)
- Per-connection queuing (isolation between clients)
- Priority-based message scheduling
- Configurable batch sizes and latency limits

## Problem Being Solved

When server sends messages to clients:
- Fast server can overwhelm slow client
- Multiple clients need fair resource allocation
- High-priority messages should jump the queue
- Need to batch small messages for efficiency
- Client should control its receive rate

## HYDRA Implementation

### Credit-Based Flow Control

**Concept:** Client grants "credits" to server. Each message consumes 1 credit. Server stops sending when credits exhausted.

**File:** `Platform/Core Gateways/VisorGateway/VisorConnectionQueue.cs` (158 lines)

```csharp
public sealed class VisorConnectionQueue
{
    private readonly ConcurrentQueue<HydraEnvelope> _queue = new();
    private int _credit = 0;
    private int _maxSize = 1000;

    public bool TryEnqueue(HydraEnvelope envelope)
    {
        if (_queue.Count >= _maxSize)
        {
            Logger.LogWarning($"[VISOR-QUEUE] Queue full for connection {ConnectionId}");
            return false;  // Reject, apply backpressure
        }

        _queue.Enqueue(envelope);
        return true;
    }

    public HydraEnvelope? TryDequeue()
    {
        // Only dequeue if credits available
        if (_credit <= 0)
            return null;

        if (_queue.TryDequeue(out var envelope))
        {
            Interlocked.Decrement(ref _credit);  // Consume credit
            return envelope;
        }

        return null;
    }

    public void AddCredit(int amount)
    {
        Interlocked.Add(ref _credit, amount);
        Logger.LogDebug($"[VISOR-QUEUE] Credit added: {amount}, total: {_credit}");
    }

    public int AvailableCredit => _credit;
    public int QueueDepth => _queue.Count;
}
```

**Key Features:**
- **Thread-safe:** Uses `ConcurrentQueue` and `Interlocked` operations
- **Bounded queue:** Rejects messages when full (backpressure)
- **Credit accounting:** Atomic decrement on dequeue

### Per-Connection Queuing

**File:** `Platform/Core Gateways/VisorGateway/VisorGateway.cs` (384 lines)

```csharp
public sealed class VisorGateway : GatewayServerBase
{
    private readonly ConcurrentDictionary<string, VisorConnectionQueue> _connectionQueues = new();
    private readonly PulseScheduler _pulseScheduler;

    private async Task HandleConnection(WebSocket webSocket, string connectionId)
    {
        // Create per-connection queue
        var queue = new VisorConnectionQueue(connectionId);
        _connectionQueues[connectionId] = queue;

        // Start sender and receiver concurrently
        var sendTask = SendLoop(webSocket, connectionId, queue);
        var receiveTask = ReceiveLoop(webSocket, connectionId);

        await Task.WhenAny(sendTask, receiveTask);

        // Cleanup
        _connectionQueues.TryRemove(connectionId, out _);
        _pulseScheduler.ClearConnectionQueues(connectionId);
    }

    private async Task SendLoop(WebSocket ws, string connectionId, VisorConnectionQueue queue)
    {
        while (ws.State == WebSocketState.Open)
        {
            HydraEnvelope? envelope = null;

            // Priority 1: Check regular queue
            envelope = queue.TryDequeue();

            // Priority 2: Check pulse scheduler (delayed/batched messages)
            if (envelope == null)
            {
                var pulses = _pulseScheduler.GetDuePulses(connectionId, queue.AvailableCredit);
                envelope = pulses.FirstOrDefault();
            }

            if (envelope != null)
            {
                var json = await _deterministicWriter.WriteAsync(envelope);
                await ws.SendAsync(
                    Encoding.UTF8.GetBytes(json),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);

                IncrementReplies();  // Stats tracking
            }
            else
            {
                await Task.Delay(10);  // No messages, brief pause
            }
        }
    }

    private async Task ReceiveLoop(WebSocket ws, string connectionId)
    {
        while (ws.State == WebSocketState.Open)
        {
            var buffer = new byte[4096];
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // Parse message
            var envelopes = VisorParsePipeline.ExtractAndParse(message);

            // Extract credit from flow envelope
            foreach (var envelope in envelopes)
            {
                if (envelope.Flow?.Credit != null)
                {
                    var queue = _connectionQueues[connectionId];
                    queue.AddCredit(envelope.Flow.Credit.Value);
                }
            }

            // Dispatch to services
            EnvelopesReceived?.Invoke(this, new EnvelopesReceivedEventArgs(connectionId, envelopes));

            TouchConnection(connectionId);  // Activity tracking
        }
    }
}
```

**Flow:**
1. **Client → Server:** Message with `Flow: { credit: 10 }`
2. **Server:** Adds 10 credits to connection queue
3. **Server → Client:** Sends up to 10 messages (consumes credits)
4. **Server:** Stops sending when credits exhausted
5. **Client:** Sends more credits when ready to receive

---

### Pulse Scheduling (Delayed/Batched Messages)

**File:** `Platform/Core Gateways/VisorGateway/PulseScheduler.cs` (148 lines)

**Status:** ⚠️ Main engine DORMANT (sophisticated batching not active)

**File:** `Platform/Core Gateways/VisorGateway/PulseScheduler.Shims.cs` (154 lines)

**Status:** ✅ ACTIVE (simpler priority-based scheduling)

```csharp
// PulseScheduler.Shims.cs - Current Active Implementation
public sealed partial class PulseScheduler
{
    public void SchedulePulse(string connectionId, HydraEnvelope envelope, int maxLatencyMs)
    {
        var queue = _connectionPulseQueues.GetOrAdd(connectionId, _ => new ConcurrentQueue<PulseItem>());

        queue.Enqueue(new PulseItem
        {
            Envelope = envelope,
            ScheduledAt = DateTimeOffset.UtcNow,
            MaxLatencyMs = maxLatencyMs,
            Priority = envelope.Qos?.Priority ?? 5  // Default priority = 5
        });
    }

    public List<HydraEnvelope> GetDuePulses(string connectionId, int availableCredit)
    {
        if (!_connectionPulseQueues.TryGetValue(connectionId, out var queue))
            return new List<HydraEnvelope>();

        var now = DateTimeOffset.UtcNow;
        var duePulses = new List<PulseItem>();

        // Dequeue all items
        while (queue.TryDequeue(out var pulse))
        {
            var age = (now - pulse.ScheduledAt).TotalMilliseconds;

            // Check if due (exceeded max latency)
            if (age >= pulse.MaxLatencyMs)
            {
                duePulses.Add(pulse);
            }
            else
            {
                // Not due yet, re-enqueue
                queue.Enqueue(pulse);
                break;  // Queue is time-ordered, rest not due
            }
        }

        // Sort by priority (lower number = higher priority)
        duePulses.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // Take up to availableCredit messages
        return duePulses
            .Take(availableCredit)
            .Select(p => p.Envelope)
            .ToList();
    }
}
```

**Current Behavior:**
- Schedules messages with max latency constraint
- Enforces latency limit (sends when deadline reached)
- Sorts by priority within due messages
- Respects credit limits

**Dormant Features (in PulseScheduler.cs main):**
- Byte-based batching (combine small messages)
- Credit accrual (fair-share across connections)
- Adaptive batching (learn optimal batch size)

---

### Client-Side Buffering

**File:** `Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs` (896 lines)

```csharp
public sealed class ChatGPTVisor
{
    private readonly List<HydraEnvelope> _downstreamBuffer = new();
    private const int MicroBatchWindowMs = 75;

    private async Task OnVisorMessage(HydraEnvelope envelope)
    {
        // Buffer downstream messages during LLM generation
        _downstreamBuffer.Add(envelope);

        // Start micro-batch timer if not already running
        if (!_microBatchTimerRunning)
        {
            _microBatchTimerRunning = true;
            _ = Task.Delay(MicroBatchWindowMs).ContinueWith(_ => FlushDownstream());
        }
    }

    private void NotifyGenerationComplete()
    {
        // LLM finished generating, flush buffered messages immediately
        FlushDownstream();
    }

    private void FlushDownstream()
    {
        if (_downstreamBuffer.Count == 0)
            return;

        var messages = _downstreamBuffer.ToList();
        _downstreamBuffer.Clear();
        _microBatchTimerRunning = false;

        // Inject all buffered messages into ChatGPT response stream
        foreach (var msg in messages)
        {
            InjectIntoChatStream(msg);
        }

        // Grant more credits to server
        SendCreditUpdate(credits: 10);
    }
}
```

**Client Batching Strategy:**
- Buffer messages during LLM generation (don't interrupt)
- Flush on generation complete OR 75ms timeout
- Send credit updates after flush

---

## Pattern Structure

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                  Client (ChatGPTVisor)                   │
│  - Buffers downstream messages (75ms micro-batch)       │
│  - Sends credit updates                                 │
└────────────┬────────────────────────────────────────────┘
             │
             │ WebSocket (bidirectional)
             │
┌────────────┴────────────────────────────────────────────┐
│              VisorGateway (Server)                       │
│  ┌─────────────────────────────────────────────────┐    │
│  │  Per-Connection Queues                          │    │
│  │  ┌──────────────┐  ┌──────────────┐            │    │
│  │  │ Conn A Queue │  │ Conn B Queue │  ...       │    │
│  │  │ Credit: 10   │  │ Credit: 0    │            │    │
│  │  │ Depth: 5     │  │ Depth: 20    │            │    │
│  │  └──────────────┘  └──────────────┘            │    │
│  └─────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────┐    │
│  │  PulseScheduler (Priority Queue)                │    │
│  │  - Delayed messages with max latency            │    │
│  │  - Priority sorting (1=highest, 10=lowest)      │    │
│  │  - Credit-aware dequeue                         │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

### Message Flow with Credits

```
1. Client starts (no credits granted)
   Server: Can't send (credit = 0)

2. Client sends: { flow: { credit: 10 } }
   Server: queue.AddCredit(10)

3. Server sends 3 messages
   Server: credit = 10 → 7 (consumed 3)

4. Server tries to send 4th message
   Server: credit = 7, queue has 10 messages → sends

5. Eventually credit = 0
   Server: SendLoop returns null, pauses

6. Client sends: { flow: { credit: 5 } }
   Server: credit = 5, resumes sending

7. Pulse with high priority arrives
   PulseScheduler: Insert at front, send when credit available
```

---

## Key Design Decisions

### 1. Credit-Based vs Window-Based Flow Control
**Decision:** Credit-based (receiver controls send rate)

**Rationale:**
- Client knows its processing capacity
- Adapts to varying client speed
- Prevents bufferbloat on slow clients

**Alternative:** Window-based (fixed send window) - less adaptive

### 2. Per-Connection Queuing
**Decision:** Each connection gets own queue

**Rationale:**
- Isolation: slow client doesn't block fast clients
- Fair resource allocation
- Independent credit accounting

**Tradeoff:** Higher memory usage (O(connections) queues)

### 3. Bounded Queues
**Decision:** Reject messages when queue full

**Rationale:**
- Apply backpressure to producer (service layer)
- Prevent unbounded memory growth
- Fast clients don't subsidize slow clients

**Alternative:** Unbounded queue - risk OOM

### 4. Priority Scheduling
**Decision:** Sort due pulses by priority before sending

**Rationale:**
- High-priority messages (errors, alerts) jump queue
- QoS differentiation
- Latency-sensitive messages get preferential treatment

**Implementation:** Lines 331-337 of VisorGateway.cs, PulseScheduler.Shims.cs

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define Connection Queue

```csharp
public class ConnectionQueue<T>
{
    private readonly ConcurrentQueue<T> _queue = new();
    private int _credit = 0;
    private readonly int _maxSize;

    public ConnectionQueue(int maxSize = 1000)
    {
        _maxSize = maxSize;
    }

    public bool TryEnqueue(T item)
    {
        if (_queue.Count >= _maxSize)
            return false;  // Backpressure

        _queue.Enqueue(item);
        return true;
    }

    public T? TryDequeue()
    {
        if (_credit <= 0)
            return default;

        if (_queue.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _credit);
            return item;
        }

        return default;
    }

    public void AddCredit(int amount)
    {
        Interlocked.Add(ref _credit, amount);
    }

    public int AvailableCredit => _credit;
    public int QueueDepth => _queue.Count;
}
```

### Step 2: Integrate with Send Loop

```csharp
public class MessageSender
{
    private readonly ConcurrentDictionary<string, ConnectionQueue<Message>> _queues = new();

    public async Task SendLoop(WebSocket ws, string connectionId)
    {
        var queue = _queues.GetOrAdd(connectionId, _ => new ConnectionQueue<Message>());

        while (ws.State == WebSocketState.Open)
        {
            var message = queue.TryDequeue();

            if (message != null)
            {
                await ws.SendAsync(Serialize(message), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                await Task.Delay(10);  // No messages, pause briefly
            }
        }
    }

    public void EnqueueMessage(string connectionId, Message message)
    {
        if (_queues.TryGetValue(connectionId, out var queue))
        {
            if (!queue.TryEnqueue(message))
            {
                // Queue full, apply backpressure
                throw new InvalidOperationException("Queue full, slow down producer");
            }
        }
    }

    public void HandleCreditUpdate(string connectionId, int credit)
    {
        if (_queues.TryGetValue(connectionId, out var queue))
        {
            queue.AddCredit(credit);
        }
    }
}
```

### Step 3: Client Credit Management

```csharp
public class MessageReceiver
{
    private int _processingCapacity = 10;

    public async Task Start(WebSocket ws)
    {
        // Grant initial credits
        await SendCreditUpdate(ws, _processingCapacity);

        while (ws.State == WebSocketState.Open)
        {
            var buffer = new byte[4096];
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

            var message = Deserialize(buffer, result.Count);

            // Process message
            await ProcessMessage(message);

            // Replenish credit after processing
            await SendCreditUpdate(ws, 1);
        }
    }

    private async Task SendCreditUpdate(WebSocket ws, int credit)
    {
        var creditMessage = new { flow = new { credit } };
        var json = JsonSerializer.Serialize(creditMessage);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
```

---

## Tradeoffs & Constraints

### Advantages
✅ Prevents server overwhelming slow clients
✅ Fair resource allocation across connections
✅ Priority support for critical messages
✅ Bounded memory usage
✅ Adaptive to client speed

### Limitations
⚠️ Credit protocol adds overhead (extra messages)
⚠️ Per-connection queues increase memory footprint
⚠️ Bounded queues may drop messages (need retry logic)
⚠️ Tuning batch windows requires experimentation

### When NOT to Use This Pattern
❌ Unicast (single client) - flow control unnecessary
❌ Low-frequency messages (< 1/second) - overhead not justified
❌ Guaranteed delivery required (need persistent queue + ACKs)
❌ Simple request/response (HTTP is sufficient)

---

## Related Patterns

- **Producer-Consumer:** Connection queue is producer-consumer buffer
- **Backpressure:** Bounded queue signals producer to slow down
- **Priority Queue:** PulseScheduler implements priority scheduling
- **Token Bucket:** Credit system similar to token bucket rate limiting

---

## Gen2 Evolution Notes

**Current (Gen1):** In-memory queues, simple credit accounting

**Future (Gen2):**
- Akka.NET actors for per-connection queue management
- EventStoreDB for durable queues (persist on server restart)
- More sophisticated batching (byte-based, adaptive windows)
- Credit accrual with fair-share scheduling

**Migration Strategy:**
- Keep credit protocol in envelope (Flow.Credit)
- Replace queue implementation (Akka mailboxes)
- Maintain backpressure semantics

---

**Last Updated:** 2025-11-10
**Pattern Stability:** High - credit protocol unlikely to change
**Code References:** VisorConnectionQueue.cs:1-158, VisorGateway.cs:318-373, PulseScheduler.Shims.cs:1-154
