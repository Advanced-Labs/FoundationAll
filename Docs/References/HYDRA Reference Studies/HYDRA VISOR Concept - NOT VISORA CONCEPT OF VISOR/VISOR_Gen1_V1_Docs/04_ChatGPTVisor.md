# 04 - ChatGPTVisor Deep Dive

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

**File:** `Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs`
**Lines:** 896
**Status:** ✅ Operational (Sprint 9.1)

ChatGPTVisor is the client-side Web Visor harness for ChatGPT with a SendArbiter state machine managing bidirectional message flow.

---

## 4.1 Architecture

### Purpose

Client-side harness that:
1. Intercepts ChatGPT SSE stream via DevTools Protocol
2. Detects VISOR fences in LLM output
3. Parses to Envelopes via VisorParsePipeline
4. Manages bidirectional flow with state machine
5. Buffers downstream messages during generation

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      ChatGPTVisor                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────┐        ┌──────────────────┐          │
│  │  DevTools        │───────▶│  SSE Parser      │          │
│  │  Protocol        │        │  (JSON Patch)    │          │
│  └──────────────────┘        └──────────────────┘          │
│           │                           │                      │
│           │                           ▼                      │
│           │                  ┌──────────────────┐          │
│           │                  │  VISOR Fence     │          │
│           │                  │  Detection       │          │
│           │                  └──────────────────┘          │
│           │                           │                      │
│           │                           ▼                      │
│           │                  ┌──────────────────┐          │
│           │                  │  VisorParse      │          │
│           │                  │  Pipeline        │          │
│           │                  └──────────────────┘          │
│           │                           │                      │
│           │                           ▼                      │
│           │         ┌────────────────────────────┐         │
│           └────────▶│   SendArbiter State       │         │
│                     │   Machine                  │         │
│                     │   (Idle/Gen/Linger)        │         │
│                     └────────────────────────────┘         │
│                               │         │                    │
│                       ┌───────┘         └───────┐          │
│                       ▼                         ▼          │
│            ┌─────────────────┐      ┌─────────────────┐   │
│            │  Upstream Queue │      │ Downstream      │   │
│            │  (LLM→Gateway)  │      │ Buffer          │   │
│            │  75ms batch     │      │ (Gateway→LLM)   │   │
│            └─────────────────┘      │ 200ms linger    │   │
│                     │                └─────────────────┘   │
│                     │                         │             │
└─────────────────────┼─────────────────────────┼─────────────┘
                      ▼                         ▼
              VisorGateway              AIChatBrowser
              (WebSocket)              (SendPromptAsync)
```

### Key Components

| Component | Lines | Purpose |
|-----------|-------|---------|
| **DevTools Protocol** | 238-265 | Intercepts ChatGPT network responses |
| **SSE Parser** | 438-489 | Extracts message text from Server-Sent Events |
| **SendArbiter State Machine** | 32-558 | Manages generation lifecycle |
| **Upstream Micro-batch** | 63-67, 571-602 | Batches LLM→Gateway messages |
| **Downstream Buffer** | 69-73, 638-738 | Holds Gateway→LLM during generation |
| **Linger Timer** | 72-73, 703-738 | Delays downstream flush |

---

## 4.2 SendArbiter State Machine

### States

```csharp
private enum ArbiterState
{
    Idle,        // Model is idle, no generation happening
    Generating,  // Model is actively generating tokens
    Linger       // Model just finished, waiting before flushing downstream
}
```

**Source:** Lines 37-47

### State Diagram

```
                     ┌─────────────────────────────────────┐
                     │                                     │
                     │  Stream detected                    │  Linger timer
                     │  (OnStreamResponseReceived)         │  expires (200ms)
                     │                                     │
                     ▼                                     │
       ┌─────────────────────────────┐                   │
       │                             │                   │
       │         GENERATING          │                   │
       │                             │                   │
       │  - Buffer downstream        │                   │
       │  - Queue upstream (75ms)    │                   │
       │  - Accumulate LLM tokens    │                   │
       │                             │                   │
       └─────────────────────────────┘                   │
                     │                                     │
                     │  message_stream_complete            │
                     │  detected                           │
                     ▼                                     │
       ┌─────────────────────────────┐                   │
       │                             │                   │
       │          LINGER             │                   │
       │                             │                   │
       │  - Continue buffering       │                   │
       │  - Start 200ms timer        │                   │
       │  - Wait for quiet period    │                   │
       │                             │                   │
       └─────────────────────────────┘                   │
                     │                                     │
                     └─────────────────────────────────────┘
                     │                                     ▲
                     │  Flush downstream,                  │
                     │  transition to Idle                 │
                     ▼                                     │
       ┌─────────────────────────────┐                   │
       │                             │                   │
       │           IDLE              │───────────────────┘
       │                             │
       │  - Send downstream immediate │
       │  - No buffering             │
       │  - Ready for next turn      │
       │                             │
       └─────────────────────────────┘
```

### Transition Table

| From | Event | To | Action | Code Line |
|------|-------|----|----- --|-----------|
| **Idle** | Stream detected | Generating | Cancel linger timer | 285, 507 |
| **Generating** | `message_stream_complete` | Linger | Start 200ms linger timer, call `NotifyGenerationComplete()` | 398-408 |
| **Generating** | Another stream starts | Generating | Stay, continue buffering | 285 |
| **Linger** | Timer expires (200ms) | Idle | Flush downstream, call `TransitionToIdle()` | 731 |
| **Linger** | New stream starts | Generating | Cancel timer, transition | 506-508 |
| **Idle** | Downstream message | Idle | Send immediately (no buffer) | 678-690 |

### Implementation Methods

**TransitionToGenerating()** (Lines 495-510)
```csharp
private void TransitionToGenerating()
{
    lock (_stateLock)
    {
        var oldState = _state;
        _state = ArbiterState.Generating;

        if (oldState != ArbiterState.Generating)
        {
            Logger.LogDebug($"SendArbiter: {oldState} → Generating", "Visor");

            // Cancel any pending linger flush
            _lingerTimer?.Stop();
        }
    }
}
```

**TransitionToLinger()** (Lines 512-528)
```csharp
private void TransitionToLinger()
{
    lock (_stateLock)
    {
        var oldState = _state;
        _state = ArbiterState.Linger;

        if (oldState != ArbiterState.Linger)
        {
            Logger.LogDebug($"SendArbiter: {oldState} → Linger", "Visor");

            // Start linger timer
            _lingerTimer?.Stop();
            _lingerTimer?.Start();
        }
    }
}
```

**NotifyGenerationComplete()** (Lines 548-557)
```csharp
public void NotifyGenerationComplete()
{
    lock (_stateLock)
    {
        if (_state == ArbiterState.Generating)
        {
            TransitionToLinger();
        }
    }
}
```

---

## 4.3 Upstream Flow (LLM → Gateway)

### Flow Path

```
ChatGPT SSE → DevTools → Extract Text → Detect Fence → Parse → Queue → Batch (75ms) → WebSocket → Gateway
```

### Micro-batch Timer

**Interval:** 75ms (configurable range: 50-100ms)

```csharp
private const int MicroBatchIntervalMs = 75; // 50-100ms range
```

**Source:** Line 67

### Flow Steps

**1. Stream Interception** (Lines 267-336)

```csharp
// Enable Fetch domain for stream interception
var fetchEnableParams = @"{
    ""patterns"": [
        {
            ""urlPattern"": ""*chatgpt.com/backend-api/*"",
            ""requestStage"": ""Response""
        }
    ]
}";

await _webView.CallDevToolsProtocolMethodAsync("Fetch.enable", fetchEnableParams);
```

- Uses DevTools Protocol `Fetch.enable`
- Intercepts `*chatgpt.com/backend-api/*` responses
- Captures at `Response` stage (after server sends)

**2. SSE Parsing** (Lines 338-432)

Extracts message text from ChatGPT's JSON Patch format:

```json
{
  "v": [
    {
      "p": "/message/content/parts/0",
      "o": "append",
      "v": "text content here"
    }
  ]
}
```

**Algorithm** (Lines 438-489):
1. Split by `\n` to get SSE lines
2. Find `event: delta` lines
3. Parse following `data: {...}` line
4. Extract `v[*].v` where `o == "append"` and `p == "/message/content/parts/0"`
5. Accumulate text deltas

**3. VISOR Fence Detection** (Lines 358-394)

```csharp
// Search for VISOR blocks starting from last processed position
var searchText = _currentStreamBuffer.ToString();
var matches = _visorFenceRegex.Matches(searchText, _lastProcessedVisorIndex);

foreach (Match match in matches)
{
    if (match.Success && match.Index >= _lastProcessedVisorIndex)
    {
        var visorContentWithFences = match.Value;  // Full match WITH fences
        var envelopes = VisorParsePipeline.ExtractAndParse(visorContentWithFences);

        if (envelopes.Count > 0)
        {
            EnqueueUpstream(envelopes);
        }

        // Mark this block as processed
        _lastProcessedVisorIndex = match.Index + match.Length;
    }
}
```

**Optimizations:**
- Only search from last processed index (avoid re-processing)
- Smart buffer trimming: Keep 100KB, trim after 50KB processed (line 416)

**4. Enqueue** (Lines 563-569)

```csharp
private void EnqueueUpstream(IReadOnlyList<HydraEnvelope> envelopes)
{
    lock (_upstreamLock)
    {
        _upstreamQueue.AddRange(envelopes);
    }
}
```

**5. Flush on Timer** (Lines 571-602)

```csharp
private void OnMicroBatchTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    FlushUpstream();
}

private void FlushUpstream()
{
    List<HydraEnvelope> batch;

    lock (_upstreamLock)
    {
        if (_upstreamQueue.Count == 0)
            return;

        batch = new List<HydraEnvelope>(_upstreamQueue);
        _upstreamQueue.Clear();
    }

    try
    {
        foreach (var envelope in batch)
        {
            SendEnvelopeToGateway(envelope);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error flushing upstream batch", "Visor");
    }
}
```

---

## 4.4 Downstream Flow (Gateway → LLM)

### Flow Path

```
Gateway → WebSocket → Receive → State Check → Buffer (if Gen/Linger) or Send (if Idle) → Linger → Coalesce → DOM Injection
```

### Linger Delay

**Duration:** 200ms (configurable range: 150-250ms)

```csharp
private const int LingerDelayMs = 200; // 150-250ms range
```

**Source:** Line 73

### Flow Steps

**1. Receive from Gateway** (Lines 203-232)

```csharp
private async Task GatewayReceiveLoopAsync(CancellationToken cancellationToken)
{
    var buffer = new byte[8192];

    while (_gatewayWebSocket.State == WebSocketState.Open)
    {
        var result = await _gatewayWebSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), cancellationToken);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleDownstreamMessageAsync(message);
        }
    }
}
```

**2. State-based Buffering** (Lines 638-701)

```csharp
private async Task HandleDownstreamMessageAsync(string message)
{
    var envelope = JsonSerializer.Deserialize<HydraEnvelope>(message);

    bool shouldSendImmediately;

    lock (_stateLock)
    {
        if (_state == ArbiterState.Generating)
        {
            // Buffer while generating
            lock (_downstreamLock)
            {
                _downstreamBuffer.Add(envelope);
            }
            shouldSendImmediately = false;
        }
        else if (_state == ArbiterState.Linger)
        {
            // Still buffering during linger
            lock (_downstreamLock)
            {
                _downstreamBuffer.Add(envelope);
            }
            shouldSendImmediately = false;
        }
        else // Idle
        {
            // Send immediately
            shouldSendImmediately = true;
        }
    }

    if (shouldSendImmediately)
    {
        await SendToLLMInputAsync(new[] { envelope });
    }
}
```

**3. Linger Timer Triggers** (Lines 703-738)

```csharp
private void OnLingerTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
{
    FlushDownstream();
}

private void FlushDownstream()
{
    List<HydraEnvelope> batch;

    lock (_downstreamLock)
    {
        if (_downstreamBuffer.Count == 0)
        {
            TransitionToIdle();
            return;
        }

        batch = new List<HydraEnvelope>(_downstreamBuffer);
        _downstreamBuffer.Clear();
    }

    try
    {
        SendToLLMInputAsync(batch).Wait();
        TransitionToIdle();
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error flushing downstream batch", "Visor");
        TransitionToIdle();
    }
}
```

**4. Coalesce Envelopes** (Lines 761-814)

Wraps all buffered envelopes in a single VISOR fence:

```csharp
private string CoalesceEnvelopes(IEnumerable<HydraEnvelope> envelopes)
{
    var sb = new StringBuilder();

    // Start VISOR fence
    sb.AppendLine("```VISOR");

    bool first = true;
    foreach (var envelope in envelopes)
    {
        // Add empty line between messages
        if (!first)
        {
            sb.AppendLine();
        }
        first = false;

        // Format as response() { payload };
        if (envelope.Payload.TryGetProperty("result", out var resultProp))
        {
            var result = resultProp.ToString();
            sb.AppendLine($"response() {{ {result} }};");
        }
    }

    // End VISOR fence
    sb.AppendLine("```");

    return sb.ToString();
}
```

**Output Format:**
```
```VISOR
response() { {"echo": "ping", "receivedAt": "2025-11-10T12:00:00Z"} };

response() { {"echo": "pong", "receivedAt": "2025-11-10T12:00:01Z"} };
```
```

**5. DOM Injection** (Lines 820-874)

```csharp
private void DebouncedInjectToDOM(string message)
{
    lock (_stateLock)
    {
        _pendingDomAction = () => InjectToDOMAsync(message).Wait();

        // Restart debounce timer (150ms)
        _domDebounceTimer?.Stop();
        _domDebounceTimer?.Start();
    }
}

private async Task InjectToDOMAsync(string message)
{
    if (_browser == null)
    {
        Logger.LogError("Cannot send message - browser reference is null");
        return;
    }

    // Delegate to browser's SendPromptAsync (handles UI thread marshaling)
    var success = await _browser.SendPromptAsync(message);
}
```

---

## 4.5 Completion Detection (FIXED Sprint 9.1)

### The Problem

How does ChatGPTVisor know when ChatGPT finished generating?

### The Solution

**SSE event detection** for `message_stream_complete` (Lines 396-411)

```csharp
// Detect chat turn completion and clear buffer
if (sseChunk.Contains("\"type\": \"message_stream_complete\"") ||
    sseChunk.Contains("message_stream_complete"))
{
    Logger.LogInformation("[VISOR-STREAM] Detected message stream complete,
                          clearing buffer for next chat turn", "Visor");
    _currentStreamBuffer.Clear();
    _lastProcessedVisorIndex = 0;

    // Notify arbiter that generation is complete
    NotifyGenerationComplete();
    Logger.LogInformation("[VISOR-STREAM] Notified arbiter of generation completion,
                          will flush downstream responses after linger delay", "Visor");

    return; // Exit early, buffer cleared
}
```

### Why This Works

1. **ChatGPT SSE stream format:**
   - Content chunks: `event: delta` with message deltas
   - Completion: `event: delta` with `"type": "message_stream_complete"`

2. **Reliable signal:** This event always fires at end of generation

3. **V-blocks cannot span turns:** Safe to clear buffer between turns

### What Happens Next

1. `NotifyGenerationComplete()` called
2. State transitions: Generating → Linger
3. Linger timer starts (200ms)
4. After 200ms: Flush downstream buffer
5. State transitions: Linger → Idle

---

## 4.6 Timing Configuration

| Timer | Value | Purpose | Configurable? |
|-------|-------|---------|---------------|
| **Micro-batch** | 75ms | Upstream batching | No (hardcoded) |
| **Linger** | 200ms | Downstream quiet period | No (hardcoded) |
| **DOM debounce** | 150ms | UI thread safety | No (hardcoded) |

**Source:** Lines 67, 73, 82

**Future Enhancement:** Make these configurable via constructor or options

---

## 4.7 Known Limitations

### Hardcoded Timings
- Micro-batch: 75ms (line 67) - not configurable
- Linger: 200ms (line 73) - not configurable
- DOM debounce: 150ms (line 82) - not configurable

### Buffer Management
- Max buffer: 100KB before trimming (line 416)
- Trim trigger: 50KB processed (line 416)
- No hard limit on buffer size (could grow unbounded)

### WebSocket
- Single connection to gateway (no failover)
- No reconnection logic on disconnect
- No exponential backoff on errors

### DevTools Protocol
- Requires specific URL: `*chatgpt.com/backend-api/*`
- Breaks if ChatGPT changes API endpoint
- No fallback if DevTools unavailable

### Error Handling
- Malformed VISOR blocks skipped (logged)
- SSE parse errors logged but ignored
- No retry logic for failed sends

---

**See Also:**
- [03 - Visor Types](./03_Visor_Types.md) - Web vs Terminal Visor comparison
- [05 - VisorGateway](./05_VisorGateway.md) - Server-side counterpart
- [06 - Parsing Components](./06_Parsing_Components.md) - How fences are parsed
