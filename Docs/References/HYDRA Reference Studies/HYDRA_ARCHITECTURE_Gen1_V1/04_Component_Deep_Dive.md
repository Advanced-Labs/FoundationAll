# 4. Component Deep Dive

Detailed analysis of core HYDRA components with implementation specifics.

## 4.1 ChatGPTVisor (Client-Side)

**File:** `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs` (896 lines)
**Status:** 🚧 In Progress

### DevTools Protocol Interception

**Setup (Lines 238-265):**
```csharp
await _webView.CallDevToolsProtocolMethodAsync("Fetch.enable", "{}");
await _webView.CallDevToolsProtocolMethodAsync("Fetch.RequestPaused", handler);
```

**Stream Response Detection (Lines 267-336):**
- Intercepts `Fetch.requestPaused` events
- Filters for ChatGPT backend API: `url.EndsWith("/backend-api/f/conversation")`
- Fetches response body via DevTools Protocol
- Processes Server-Sent Events (SSE) format

### State Machine: SendArbiter

**Enum (Lines 37-47):**
```csharp
private enum ArbiterState
{
    Idle,       // Model idle, no generation
    Generating, // Model actively generating tokens
    Linger      // Just finished, waiting before flush
}
```

**State Transitions:**

| From | To | Trigger | Line |
|------|-----|---------|------|
| Idle | Generating | Stream response detected | 285 |
| Generating | Linger | Completion detected | 554 |
| Linger | Idle | Linger timer elapsed + flush complete | 716, 731 |

**State Behavior:**
- **Idle:** Downstream messages sent immediately (Line 678-683)
- **Generating:** Downstream messages buffered (Line 656-665)
- **Linger:** Downstream messages buffered, timer active (Line 667-676)

### Upstream Micro-Batching (75ms)

**Timer Setup (Lines 103-106):**
```csharp
_microBatchTimer = new System.Timers.Timer(MicroBatchIntervalMs); // 75ms
_microBatchTimer.Elapsed += OnMicroBatchTimerElapsed;
_microBatchTimer.AutoReset = true;
_microBatchTimer.Start();
```

**Flush Logic (Lines 576-602):**
1. Drain `_upstreamQueue` into batch
2. Send each envelope to gateway via WebSocket
3. Log transmission count

**Purpose:** Reduce WebSocket messages by batching rapid user inputs

### Downstream Buffering (200ms Linger)

**Linger Timer (Lines 108-110):**
```csharp
_lingerTimer = new System.Timers.Timer(LingerDelayMs); // 200ms
_lingerTimer.Elapsed += OnLingerTimerElapsed;
_lingerTimer.AutoReset = false; // One-shot
```

**Buffer Management (Lines 638-701):**
```csharp
// HandleDownstreamMessageAsync
if (_state == ArbiterState.Generating || _state == ArbiterState.Linger) {
    lock (_downstreamLock) {
        _downstreamBuffer.Add(envelope);
    }
} else { // Idle
    await SendToLLMInputAsync(new[] { envelope });
}
```

**FlushDownstream (Lines 708-738):**
1. Drain buffer atomically
2. Call `SendToLLMInputAsync(batch)`
3. Transition to Idle state

**Purpose:** Wait for ChatGPT to finish generating before injecting responses

### Completion Detection (FIXED as of Sprint 9.1)

**Detection Logic (Lines 398-428 in ProcessStreamChunk):**
```csharp
if (sseChunk.Contains("\"finish_reason\":\"stop\"") || 
    sseChunk.Contains("\"is_completion\":true") ||
    sseChunk.Contains("[DONE]"))
{
    _logger.LogDebug("[VISOR] Detected generation completion", "VisorState");
    NotifyGenerationComplete();
}
```

**Fix:** Previously had race conditions; now reliably detects completion markers

---

## 4.2 VisorGateway (Server-Side)

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorGateway.cs` (384 lines)
**Status:** ⚠️ Partial (no auth)

### WebSocket Endpoint

**Configuration (Lines 74-110):**
```csharp
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://localhost:7777");
_app = builder.Build();
_app.UseWebSockets();

_app.Map("/visor", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();
        await HandleWebSocketConnection(connectionId, webSocket, CancellationToken.None);
    }
});
```

**URL:** `ws://localhost:7777/visor`
**Auth:** None (Line 34 comment: "No authentication in dev mode")

### Auth Handshake

**Current State:** NOT IMPLEMENTED
- Connections immediately accepted
- No token validation
- No session binding

**Future:** Will integrate with HydraSecurityService for OAuth tokens

### Parsing Pipeline

**Dual Format Support (Lines 246-278):**

1. **JSON Envelope Format** (from ChatGPTVisor):
```csharp
var envelope = JsonSerializer.Deserialize<HydraEnvelope>(message, HydraEnvelope.JsonOptions);
parsedEnvelopes = new[] { envelope };
```

2. **VISOR Fenced Format** (from web clients):
```csharp
var parsedEnvelopes = VisorParsePipeline.ExtractAndParse(message);
```

**Pipeline Chain:**
```
Raw Message
  → VisorParsePipeline.ExtractAndParse
  → VBlockPreParser.Extract (regex: ```\s*VISOR\s*[\r\n]+(.*?)```)
  → Json1sParser.Parse (prolog + body → envelope)
  → List<HydraEnvelope>
```

### Connection Management

**Connection Registry (Line 40):**
```csharp
private readonly ConcurrentDictionary<string, VisorConnectionQueue> _connectionQueues = new();
```

**Queue Creation (Lines 174-175):**
```csharp
var queue = new VisorConnectionQueue(connectionId, initialCredit: 10, maxQueueSize: 1000);
_connectionQueues[connectionId] = queue;
```

**Cleanup (Lines 222-227):**
```csharp
_connectionQueues.TryRemove(connectionId, out _);
_pulseScheduler.ClearConnection(connectionId);
```

### Pulse Scheduling Integration

**Scheduler Instantiation (Lines 63-67):**
```csharp
_pulseScheduler = new PulseScheduler(new PulseSchedulerOptions
{
    MaxLatencyMs = 5000,
    MaxBytes = 65536
});
```

**Currently Uses Shim API Only:**
- `Schedule(connectionId, envelope, priority)` (Line 166)
- `GetDuePulses(connectionId, maxCount)` (Line 332)
- `ClearConnection(connectionId)` (Line 224)

Main engine features (fair-share, credit accrual) not fully utilized.

---

## 4.3 VisorHydraService

**File:** `/src/Hydra/Hydra.Server/Platform/Core Services/VisorHydraService/VisorHydraService.cs` (304 lines)
**Status:** ⚠️ Partial

### MessageRouter Pattern

**Initialization (Lines 30-37):**
```csharp
private readonly MessageRouter _router;

public VisorHydraService()
{
    _router = new MessageRouter(this);
}
```

### Op/Subject Dispatch

**Handler Discovery (MessageRouter.cs Lines 155-175):**
```csharp
var methods = _target.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
foreach (var method in methods)
{
    var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
    if (attr != null)
    {
        var key = BuildKey(attr.Op, attr.Subject);
        _handlers[key] = method;
    }
}
```

**Routing Logic (Lines 53-66):**
```csharp
// Try op:subject first
var key = BuildKey(op, subject);
if (!_handlers.TryGetValue(key, out var method))
{
    // Fallback to op-only
    key = BuildKey(op, null);
    if (!_handlers.TryGetValue(key, out method))
    {
        throw new InvalidOperationException($"No handler for op='{op}', subject='{subject}'");
    }
}
```

### Echo Handler (Working Example)

**Handler Method (Lines 149-262):**
```csharp
[MessageHandler("mcp")]
public async Task<object?> HandleMcp(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    // Extract envelope
    var envelope = parameters["_envelope"] as HydraEnvelope;
    
    // Extract method name
    var method = GetMethodName(envelope, parameters);
    
    // For echo: return result directly (Line 228-250)
    if (method == "echo")
    {
        var message = mcpParams.TryGetValue("message", out var msg) ? msg?.ToString() :
                      mcpParams.TryGetValue("text", out var txt) ? txt?.ToString() :
                      "No message provided";
        
        return new {
            echo = message,
            receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            envelopeId = envelope.Id,
            method = method,
            status = "success"
        };
    }
    
    // For other methods: delegate to XmcpClientHydraService
}
```

### Return Envelope Creation

**Handled in App.xaml.cs Event Handler (Lines 186-209):**
```csharp
var responseEnvelope = new HydraEnvelope(
    Id: Guid.NewGuid().ToString(),
    Type: HydraEnvelopeType.@return,
    Ts: DateTimeOffset.UtcNow,
    Src: "VisorHydraService",
    Dst: requestEnvelope.Src ?? args.ConnectionId,
    Op: "response",
    Subject: requestEnvelope.Op,
    Corr: requestEnvelope.Id,  // Correlation to request
    Trace: null,
    Auth: null,
    Caps: null,
    Qos: null,
    Flow: null,
    Schema: null,
    ContentType: "application/json",
    Payload: JsonSerializer.SerializeToElement(new {
        requestId = requestEnvelope.Id,
        result = responseResult
    }),
    Attachments: null,
    Error: null
);
```

---

## 4.4 PulseScheduler (Batching Engine)

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/PulseScheduler.cs` (148 lines)
**Status:** ⚠️ Partial (main engine underutilized)

### Main Engine (Sophisticated, Partially Active)

**ProcessAsync Loop (Lines 79-97):**
```csharp
while (!_cancellationToken.IsCancellationRequested)
{
    await Task.Delay(_options.MaxLatencyMs, _cancellationToken);
    
    if (_currentBytes >= _options.MaxBytes || HasOverdueItems())
    {
        await FlushAsync();
    }
}
```

**FlushAsync Method (Lines 99-127):**
```csharp
private async Task FlushAsync()
{
    var itemsToFlush = new List<PulseItem>();
    var bytesToFlush = 0;

    while (_queue.TryDequeue(out var item))
    {
        itemsToFlush.Add(item);
        bytesToFlush += item.Size;
        
        if (bytesToFlush >= _options.MaxBytes)
            break; // Byte limit reached
    }

    if (itemsToFlush.Count == 0)
        return;

    Interlocked.Add(ref _currentBytes, -bytesToFlush);

    if (_options.FairShare)
    {
        Interlocked.Decrement(ref _credit);
    }

    _logger.LogDebug("Flushing {Count} items ({Bytes} bytes)", itemsToFlush.Count, bytesToFlush);

    await Task.Delay(1); // Placeholder for actual delivery logic
}
```

**Note:** Line 126 has placeholder `await Task.Delay(1)` - main engine coded but delivery not fully implemented.

### Shim API (Simple, Active)

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/PulseScheduler.Shims.cs` (154 lines)

**Schedule Method (Lines 64-80):**
```csharp
public void Schedule(string connectionId, HydraEnvelope envelope, int priority)
{
    lock (_legacyLock)
    {
        _legacyPulses.Add(new LegacyPulse
        {
            ConnectionId = connectionId,
            Envelope = envelope,
            Priority = priority,
            ScheduledAt = DateTimeOffset.UtcNow
        });
    }
}
```

**GetDuePulses Method (Lines 88-124):**
```csharp
public IReadOnlyList<HydraEnvelope> GetDuePulses(string connectionId, int maxCount)
{
    lock (_legacyLock)
    {
        var now = DateTimeOffset.UtcNow;
        var connectionPulses = _legacyPulses
            .Where(p => p.ConnectionId == connectionId)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.ScheduledAt)
            .Take(maxCount)
            .ToList();

        foreach (var pulse in connectionPulses)
        {
            _legacyPulses.Remove(pulse);
        }

        return connectionPulses.Select(p => p.Envelope).ToList();
    }
}
```

**Current State:** VisorGateway uses ONLY shims, main engine features dormant.

### Configuration

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/PulseSchedulerOptions.cs` (67 lines)

| Option | Default | Range | Used By |
|--------|---------|-------|---------|
| MaxLatencyMs | 100 | 1-10,000 | Main engine |
| MaxBytes | 65,536 | 1-10M | Main engine |
| MinCredit | 1 | 0-1,000 | Main engine (if FairShare) |
| FairShare | true | - | Main engine |
| QueueDepthWarn | 1,000 | 1-100,000 | Both |

**VisorGateway Configuration:** `MaxLatencyMs = 5000` (Line 65 of VisorGateway.cs)

---

## 4.5 VisorConnectionQueue

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorConnectionQueue.cs` (158 lines)
**Status:** ✅ Working

### Ring Buffer Implementation

**Storage (Line 23):**
```csharp
private readonly ConcurrentQueue<HydraEnvelope> _queue = new();
```

**Capacity Control (Lines 63-72):**
```csharp
public bool TryEnqueue(HydraEnvelope envelope)
{
    if (_queue.Count >= _maxQueueSize)
    {
        _logger.LogWarning("Queue full ({MaxSize}), dropping envelope {Id}", _maxQueueSize, envelope.Id);
        return false;
    }
    
    _queue.Enqueue(envelope);
    return true;
}
```

**Max Queue Size:** 1000 messages (configurable, Line 26)

### Credit-Based Flow Control

**Credit Management:**
- **Initial Credit:** 10 (Line 35, configurable via constructor)
- **Credit Property:** Thread-safe getter (Lines 42-51)
- **AddCredit:** Increment credit (Lines 105-114)
- **ResetCredit:** Set credit to specific amount (Lines 120-126)

**Credit-Gated Dequeue (Lines 80-98):**
```csharp
public bool TryDequeue(out HydraEnvelope? envelope)
{
    lock (_creditLock)
    {
        if (_credit <= 0 || _queue.IsEmpty)
        {
            envelope = null;
            return false; // No credit or no messages
        }

        if (_queue.TryDequeue(out envelope))
        {
            _credit--; // Consume credit
            return true;
        }

        envelope = null;
        return false;
    }
}
```

**Flow Control Logic:**
1. Client grants credit via Flow.Credit in envelope
2. Queue consumes 1 credit per dequeued message
3. When credit reaches 0, dequeue blocks
4. Prevents overwhelming slow clients

**Benefits:**
- Backpressure mechanism
- Client-controlled rate limiting
- Prevents message buildup
