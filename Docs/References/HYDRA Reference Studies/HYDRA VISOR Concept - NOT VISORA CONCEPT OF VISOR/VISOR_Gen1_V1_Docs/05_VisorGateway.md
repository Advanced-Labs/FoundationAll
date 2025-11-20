# 05 - VisorGateway Deep Dive

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

**File:** `Platform/Core Gateways/VisorGateway/VisorGateway.cs`
**Lines:** 384
**Status:** ✅ Operational (Sprint 9.1)

VisorGateway is the server-side WebSocket gateway for VISOR protocol, handling connection management, message parsing, and envelope routing.

---

## 5.1 Architecture

### Purpose

Server-side gateway that:
1. Accepts WebSocket connections on `/visor` endpoint
2. Parses incoming messages (VISOR fenced or JSON envelope)
3. Raises `EnvelopesReceived` event for service routing
4. Sends outgoing envelopes with deterministic serialization
5. Manages per-connection queues and credit

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        VisorGateway                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  WebSocket                                                       │
│  Endpoint:7777/visor                                            │
│       │                                                          │
│       ├──▶ Per-Connection State                                 │
│       │    - VisorConnectionQueue (ring buffer)                 │
│       │    - Credit: 10 initial                                 │
│       │                                                          │
│       ├──▶ Receive Loop ──────────┐                            │
│       │                            │                             │
│       └──▶ Send Loop              │                             │
│                                    ▼                             │
│                          ┌──────────────────┐                   │
│                          │  Format          │                   │
│                          │  Detection       │                   │
│                          └──────────────────┘                   │
│                           │             │                        │
│              Starts with '{'          Starts with '```'         │
│                           │             │                        │
│                           ▼             ▼                        │
│                  ┌─────────────┐  ┌─────────────────┐          │
│                  │ JSON        │  │ VISOR            │          │
│                  │ Deserialize │  │ Parse Pipeline   │          │
│                  └─────────────┘  └─────────────────┘          │
│                           │             │                        │
│                           └──────┬──────┘                        │
│                                  ▼                               │
│                         ┌──────────────────┐                    │
│                         │  Envelopes       │                    │
│                         │  Received Event  │                    │
│                         └──────────────────┘                    │
│                                  │                               │
│                                  ▼                               │
│                         ┌──────────────────┐                    │
│                         │ PulseScheduler   │                    │
│                         │ (Shims API)      │                    │
│                         └──────────────────┘                    │
│                                  │                               │
│                                  ▼                               │
│                         ┌──────────────────┐                    │
│                         │ Deterministic    │                    │
│                         │ Writer           │                    │
│                         └──────────────────┘                    │
│                                  │                               │
└──────────────────────────────────┼──────────────────────────────┘
                                   ▼
                         WebSocket Send (JSON)
```

---

## 5.2 Parsing Pipeline

### Dual-Format Support

VisorGateway accepts two message formats:

#### Format 1: VISOR Fenced Block

**Detection:** Message starts with ` ``` ` (line 248)

```
```VISOR
mcp() { { "name": "echo", "message": "ping" } };
```
```

**Parsing Path** (Lines 273-278):
```csharp
// Message is VISOR fenced block format
var parsedEnvelopes = VisorParsePipeline.ExtractAndParse(message);
envelopes = new List<HydraEnvelope>(parsedEnvelopes);
```

**Pipeline:**
```
VisorParsePipeline.ExtractAndParse(message)
  └─> VBlockPreParser.Extract()      // Extract V-blocks from fences
      └─> Json1sParser.Parse()       // Parse each V-block
          └─> List<HydraEnvelope>
```

#### Format 2: JSON Envelope

**Detection:** Message starts with `{` (line 248)

```json
{
  "id": "guid",
  "type": "call",
  "op": "mcp",
  "payload": { ... }
}
```

**Parsing Path** (Lines 249-270):
```csharp
// Message is JSON envelope format (from ChatGPTVisor or other parsed sources)
try
{
    var envelope = JsonSerializer.Deserialize<HydraEnvelope>(
        message, HydraEnvelope.JsonOptions);

    if (envelope != null)
    {
        envelopes = new List<HydraEnvelope> { envelope };
    }
}
catch (Exception ex)
{
    Logger.LogError(ex, "Failed to deserialize JSON envelope");
    envelopes = new List<HydraEnvelope>();
}
```

### Why Two Formats?

| Format | Use Case | Sent By |
|--------|----------|---------|
| **VISOR Fenced** | Web clients sending VISOR directly | Web browsers, CLI tools |
| **JSON Envelope** | Already-parsed envelopes | ChatGPTVisor (parsed client-side) |

---

## 5.3 Connection Management

### Connection Lifecycle

**1. Accept Connection** (Lines 106-110)

```csharp
var webSocket = await context.WebSockets.AcceptWebSocketAsync();
var connectionId = Guid.NewGuid().ToString();

await HandleWebSocketConnection(connectionId, webSocket, _cts.Token);
```

**2. Initialize Connection State** (Lines 171-176)

```csharp
AddConnection(connectionId);  // Base class tracking

// Create connection queue
var queue = new VisorConnectionQueue();
_connectionQueues[connectionId] = queue;
```

**3. Start Dual Loops** (Line 182)

- **SendLoop:** Background task for outgoing messages (lines 318-346)
- **ReceiveLoop:** Inline handling of incoming messages (lines 184-211)

**4. Process Messages** (Lines 235-316)

```csharp
private async Task ProcessIncomingMessage(
    string connectionId, string message, VisorConnectionQueue queue)
{
    // 1. Parse message (VISOR or JSON format)
    // 2. Extract credit from envelopes
    // 3. Raise EnvelopesReceived event
}
```

**5. Cleanup on Disconnect** (Lines 221-232)

```csharp
RemoveConnection(connectionId);
_connectionQueues.TryRemove(connectionId, out _);
_pulseScheduler.ClearConnection(connectionId);

if (webSocket.State == WebSocketState.Open)
{
    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
}
```

### VisorConnectionQueue

**File:** `VisorConnectionQueue.cs` (158 lines)

**Properties:**
- Ring buffer (ConcurrentQueue)
- Max size: 1000 messages (line 36)
- Initial credit: 10 messages (line 35)
- Credit consumed per dequeue (line 92)

**Methods:**
```csharp
bool TryEnqueue(HydraEnvelope envelope)    // Add message (if space available)
bool TryDequeue(out HydraEnvelope? env)     // Get message (if credit available)
void AddCredit(int amount)                  // Replenish credit
void ResetCredit(int amount)                // Reset to specific value
List<HydraEnvelope> DrainAll()              // Emergency drain
void Clear(int newCredit = 10)              // Clear and reset
```

**Credit Flow:**
1. Connection starts with 10 credit
2. Each dequeue consumes 1 credit
3. Client messages can add credit via `envelope.Flow.Credit`
4. If credit = 0, dequeue fails (backpressure)

---

## 5.4 Pulse Scheduling Integration

### Critical Discovery: Dual Implementation

PulseScheduler has **two personalities:**

#### Main Engine (DORMANT 💤)

**File:** `PulseScheduler.cs` (148 lines)

**Sophisticated Features:**
- `ConcurrentQueue<PulseItem>` buffering
- Timer-based max latency (100ms default)
- Byte-based batching (MaxBytes threshold, 65KB default)
- Credit-based flow control (MinCredit, FairShare)
- Queue depth monitoring

**BUT:** FlushAsync() has placeholder (line 126):
```csharp
// Process items (placeholder for actual delivery logic)
await Task.Delay(1); // Simulate async work
```

**This code path is NOT used by VisorGateway**

#### Shim API (ACTIVE ✅)

**File:** `PulseScheduler.Shims.cs` (154 lines)

**Simple Implementation:**
```csharp
private readonly List<ScheduledPulse> _legacyPulses = new();
private readonly object _legacyLock = new();
```

**Methods Actually Used:**

**Start() / Stop()** (Lines 45-56) - No-ops:
```csharp
public void Start(int intervalMs = 100)
{
    _logger.LogDebug("PulseScheduler Start() called (legacy API)");
}

public void Stop()
{
    _logger.LogDebug("PulseScheduler Stop() called (legacy API)");
}
```

**Schedule()** (Lines 64-80) - Adds to list:
```csharp
public void Schedule(string connectionId, HydraEnvelope envelope, int priority = 0)
{
    var scheduledPulse = new ScheduledPulse
    {
        ConnectionId = connectionId,
        Envelope = envelope,
        Priority = priority,
        ScheduledTime = DateTime.UtcNow
    };

    lock (_legacyLock)
    {
        _legacyPulses.Add(scheduledPulse);
    }
}
```

**GetDuePulses()** (Lines 88-124) - Priority sort + return:
```csharp
public List<HydraEnvelope> GetDuePulses(string connectionId, int maxCount)
{
    lock (_legacyLock)
    {
        var now = DateTime.UtcNow;
        var connectionPulses = _legacyPulses
            .Where(p => p.ConnectionId == connectionId)
            .ToList();

        // Check for overdue pulses (max latency)
        var hasOverdue = connectionPulses.Any(p =>
            (now - p.ScheduledTime).TotalMilliseconds >= _options.MaxLatencyMs);

        // Priority sort: highest first, then FIFO
        var selectedPulses = connectionPulses
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ScheduledTime)
            .Take(maxCount)
            .ToList();

        // Remove selected
        foreach (var pulse in selectedPulses)
        {
            _legacyPulses.Remove(pulse);
        }

        return selectedPulses.Select(p => p.Envelope).ToList();
    }
}
```

**ClearConnection()** (Lines 131-140) - Cleanup:
```csharp
public int ClearConnection(string connectionId)
{
    lock (_legacyLock)
    {
        var removed = _legacyPulses.RemoveAll(p => p.ConnectionId == connectionId);
        return removed;
    }
}
```

### VisorGateway Usage

**Line 116:** `_pulseScheduler.Start()` (no-op shim)
**Line 166:** `SchedulePulse()` calls shim's `Schedule()`
**Line 332:** `SendLoop()` calls shim's `GetDuePulses()`
**Line 224:** Cleanup calls shim's `ClearConnection()`

### Configuration

**PulseSchedulerOptions.cs** (66 lines):

```csharp
public record PulseSchedulerOptions
{
    public int MaxLatencyMs { get; init; } = 100;      // Overridden to 5000 in VisorGateway
    public int MaxBytes { get; init; } = 65536;        // NOT used by shims
    public int MinCredit { get; init; } = 1;           // NOT used by shims
    public bool FairShare { get; init; } = true;       // NOT used by shims
    public int QueueDepthWarn { get; init; } = 1000;   // NOT used by shims
}
```

**VisorGateway Override** (Line 65):
```csharp
var pulseOptions = Options.Create(new PulseSchedulerOptions
{
    MaxLatencyMs = 5000  // 5 seconds (not 100ms default)
});
```

---

## 5.5 Deterministic Serialization

### Why Deterministic?

- Byte-stable output for caching
- Consistent ordering for diffs
- Predictable serialization for testing

### Implementation

**VisorDeterministicWriter** (uses VisorCanonicalizer)

```csharp
public async Task<string> WriteAsync(
    HydraEnvelope envelope,
    JsonSerializerOptions options,
    CancellationToken cancellationToken = default)
{
    // 1. Serialize to JSON
    var json = JsonSerializer.Serialize(envelope, options);

    // 2. Canonicalize (sort keys)
    var canonical = _canonicalizer.CanonicalizeString(json);

    return canonical;
}
```

**VisorCanonicalizer** - Sorts JSON keys deterministically

**Used in SendEnvelope** (Line 355):
```csharp
var json = await _deterministicWriter.WriteAsync(
    envelope, HydraEnvelope.JsonOptions, cancellationToken);
```

---

## 5.6 Endpoints

### `/visor` - WebSocket Endpoint

**Path:** `/visor`
**Protocol:** WebSocket
**Port:** 7777 (configurable)
**Auth:** None (dev mode)

**Usage:**
```javascript
const ws = new WebSocket('ws://localhost:7777/visor');

ws.onopen = () => {
  ws.send('```VISOR\nmcp() { { "name": "echo" } };\n```');
};

ws.onmessage = (event) => {
  const envelope = JSON.parse(event.data);
  console.log(envelope);
};
```

### `/health` - Health Check

**Path:** `/health`
**Method:** GET
**Protocol:** HTTP
**Returns:**
```json
{
  "status": "healthy",
  "gateway": "Visor Gateway",
  "port": 7777,
  "protocol": "WS-Visor",
  "activeConnections": 3,
  "pendingPulses": 12
}
```

**Source:** Lines 83-94

---

## 5.7 Known Limitations

### WebSocket
- No authentication in dev mode (comment line 34)
- No rate limiting per connection
- No max message size enforcement
- No backpressure on client send

### Connection Management
- No connection timeout (stays open indefinitely)
- No max connections limit globally
- No connection pooling or reuse

### Message Handling
- Buffer: 8192 bytes (line 185) - fixed size
- No explicit max message size
- Large messages could cause memory issues

### PulseScheduler
- Main engine dormant (sophisticated features unused)
- Shims lack byte-based batching
- No fair-share across connections currently active
- No credit accrual logic in shims

### Error Handling
- Malformed JSON logged but connection stays open
- Parse errors don't close connection
- No circuit breaker for repeated errors

---

**See Also:**
- [04 - ChatGPTVisor](./04_ChatGPTVisor.md) - Client-side counterpart
- [06 - Parsing Components](./06_Parsing_Components.md) - VBlockPreParser + Json1sParser
- [07 - Batching & Pulsing](./07_Batching_Pulsing.md) - PulseScheduler deep dive
- [08 - Flow Control](./08_Flow_Control.md) - VisorConnectionQueue details
