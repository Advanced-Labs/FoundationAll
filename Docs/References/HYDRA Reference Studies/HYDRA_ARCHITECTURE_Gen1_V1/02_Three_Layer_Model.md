# 2. The Three-Layer Model

HYDRA's architecture is organized into three distinct layers, each with clear responsibilities and boundaries.

## 2.1 Edge Layer: VISOR (Harness-Level Framing)

### What VISOR Is
- **Purpose:** Protocol-specific framing for injecting structured messages into/from ChatGPT
- **Format:** Markdown fenced code blocks with `visor` language tag
- **Location:** Edge only (ChatGPTVisor component)
- **Scope:** Harness-level concern, not part of core HYDRA

### What VISOR Is NOT
- Not a routing protocol
- Not a batching mechanism
- Not a business logic layer
- Not required for other gateways (MCP, HTTP use native formats)

### VISOR Fence Format

**Upstream (ChatGPT → HYDRA):**
```
```visor
mcp(v="latest", c="json1s") {
  .headers = { "Priority": 5 };
  .meta = { "schema": "mcp/v1", "traceparent": "..." };
  { "name": "echo", "message": "Hello World" };
}
```
```

**Downstream (HYDRA → ChatGPT):**
```
```VISOR
response() { "echo": "Hello World", "status": "success" };
```
```

### Current Implementations

#### ChatGPTVisor (Status: 🚧 In Progress)
- **File:** `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs` (896 lines)
- **Technology:** WebView2 DevTools Protocol
- **Capabilities:**
  - Stream response interception (SSE parsing)
  - VISOR fence detection via regex
  - Bidirectional WebSocket to VisorGateway
  - SendArbiter state machine (Idle/Generating/Linger)
  - Upstream micro-batching (75ms)
  - Downstream buffering (200ms linger)
- **Connection:** `ws://localhost:7777/visor`

#### BrowserVisorAdapter (Status: 💤 Dormant)
- **File:** `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/BrowserVisorAdapter.cs` (113 lines)
- **Purpose:** Bridge between browser and VISOR
- **Status:** Coded but integration unclear

### VISOR Responsibilities vs Gateway Responsibilities

| Concern | VISOR Layer | Gateway Layer |
|---------|-------------|---------------|
| **Fence Detection** | ✅ ChatGPTVisor | ❌ |
| **Parsing to Envelope** | ❌ | ✅ VisorGateway |
| **Batching** | Timing only (75ms, 200ms) | ✅ PulseScheduler |
| **Routing** | ❌ | ✅ MessageRouter |
| **Authentication** | ❌ | ✅ (future) |
| **Flow Control** | State machine | ✅ Credit-based queue |

---

## 2.2 Transport Layer: Hydra Envelope (v0.2)

### Canonical Message Format

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Envelope/HydraEnvelope.cs` (73 lines)

```csharp
public sealed record HydraEnvelope(
    string Id,                           // Unique message ID (GUID)
    HydraEnvelopeType Type,              // call | return | event | sub | unsub | stream | pulse | error
    DateTimeOffset Ts,                   // Timestamp
    string Src,                          // Source (gateway, service, connection ID)
    string Dst,                          // Destination (service, connection ID)
    string? Op,                          // Operation (e.g., "mcp", "response")
    string? Subject,                     // Sub-operation (e.g., "echo", "list_tools")
    string? Corr,                        // Correlation ID (request ID for responses)
    HydraTrace? Trace,                   // Distributed tracing (traceparent)
    HydraAuth? Auth,                     // Authentication/authorization
    IReadOnlyList<string>? Caps,         // Capabilities
    HydraQos? Qos,                       // Quality of Service (priority, TTL, retries)
    HydraFlow? Flow,                     // Flow control (credit)
    string? Schema,                      // Payload schema identifier
    string? ContentType,                 // Payload content type
    JsonElement Payload,                 // Actual message data
    IReadOnlyList<HydraAttachment>? Attachments,  // Attachments (URI, hash, size)
    JsonElement? Error                   // Error details
);
```

### Envelope Type Enum
```csharp
public enum HydraEnvelopeType
{
    call,      // Request/invoke
    @return,   // Response
    @event,    // Pub/sub event
    sub,       // Subscribe
    unsub,     // Unsubscribe
    stream,    // Streaming data
    pulse,     // Batched messages
    error      // Error response
}
```

### Supporting Types

**HydraQos (Quality of Service):**
```csharp
record HydraQos(
    int? Priority,      // Message priority (higher = more important)
    int? Ttl,          // Time to live (ms)
    int? Retries,      // Retry attempts
    string? Ordering   // Ordering guarantee
);
```

**HydraTrace (Distributed Tracing):**
```csharp
record HydraTrace(
    string? TraceParent  // W3C Trace Context traceparent header
);
```

**HydraAuth (Authentication):**
```csharp
record HydraAuth(
    string? Presenter,     // Presenter identity
    HydraAuthProof? Proof  // Proof of authentication
);
```

**HydraFlow (Flow Control):**
```csharp
record HydraFlow(
    int? Credit  // Flow control credit
);
```

### Evolvability Contract

**Design Principles:**
1. **Add-Only:** New fields can be added without breaking old parsers
2. **Unknown Fields Ignored:** Parsers skip fields they don't recognize
3. **Optional Fields:** Most fields are nullable/optional
4. **JSON Schema Validation:** Schema file enforces structure at gateway

**Schema File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Envelope/EnvelopeSchema.json`

### VISOR → Envelope Mapping

**Parser:** `Json1sParser.cs` (388 lines)

| VISOR Section | Envelope Field | Notes |
|---------------|----------------|-------|
| `mcp(v="latest")` | `Op = "mcp"` | Prolog → Op |
| `payload.name` | `Subject` | Extracted from payload |
| `.headers.Priority` | `Qos.Priority` | Dotted section → QoS |
| `.meta.traceparent` | `Trace.TraceParent` | Dotted section → Trace |
| `.meta.schema` | `Schema` | Metadata field |
| `.meta.content_type` | `ContentType` | Metadata field |
| `{ payload JSON }` | `Payload` | Main JSON body |
| (generated) | `Id`, `Ts`, `Type` | Auto-generated at parse time |

### Envelope → CLR Binding

**Router:** `MessageRouter.cs` (216 lines)

**Binding Process:**
1. Extract `Op` and `Subject` from envelope
2. Lookup handler method via reflection: `[MessageHandler("op", "subject")]`
3. Extract `Payload` JsonElement
4. Match method parameters by name and type
5. Invoke method with bound parameters

**Example:**
```csharp
// Envelope
{
  "op": "mcp",
  "subject": "echo",
  "payload": { "name": "echo", "params": { "message": "Hello" } }
}

// Handler Method
[MessageHandler("mcp")]
public async Task<object?> HandleMcp(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    // Parameters contains deserialized payload fields
}
```

---

## 2.3 Service Layer: Protocol-Agnostic Services

### IHydraService Interface

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/IHydraService.cs`

```csharp
public interface IHydraService
{
    Task<object?> InvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken);
}
```

### HydraServiceBase Pattern

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraServiceBase.cs` (213 lines)

**Features:**
- Abstract base class for all services
- Instance management (per-session singletons)
- Stats tracking (invocation count, errors, timing)
- Protected helper methods

**Example Service:**
```csharp
[HydraService(
    Name = "VisorHydraService",
    Description = "Visor protocol service with op/subject routing",
    AutoStart = true,
    BootPriority = 5
)]
public partial class VisorHydraService : HydraServiceBase
{
    private readonly MessageRouter _router;

    public VisorHydraService()
    {
        _router = new MessageRouter(this);
    }

    [MessageHandler("mcp")]
    public async Task<object?> HandleMcp(
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        // Business logic here
        // No knowledge of VISOR or Envelope
    }
}
```

### Service Discovery and Registration

**Discovery File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRegistry.cs` (270 lines)

**Process:**
1. Assembly scan for `[HydraService]` attribute
2. Filter by `IHydraService` interface
3. Register in `ServiceRegistry.Global`
4. Auto-start based on `AutoStart` flag and `BootPriority`

**Boot Sequence:**
```csharp
// App.xaml.cs OnStartup()
ServiceRegistry.Global.Initialize(_host.Services);
ServiceRegistry.Global.DiscoverServices();

await ServiceBootManager.AutoStartHostedServicesAsync(
    ServiceRegistry.Global,
    _host.Services,
    cancellationToken
);
```

**Current Services:**
| Service | AutoStart | BootPriority | Status |
|---------|-----------|--------------|--------|
| XmcpClientHydraService | ✅ | 3 | ✅ Working |
| VisorHydraService | ✅ | 5 | ⚠️ Partial |
| HydraStoreSystemV1 | ✅ | 90 | ✅ Working |
| HydraSecurityService | ✅ | 100 | ✅ Working |
| HydraCoreService | ❌ | 5000 | ✅ Working |
| ChatGPTService | ❌ | 5000 | ✅ Working |

### Session-Based Lifecycle

**Pattern:** Per-session singletons
- Each session gets its own service instance
- Instances cached in static `ConcurrentDictionary<string, ConcurrentDictionary<string, HydraServiceBase>>`
- Key structure: `ServiceName → (SessionId → Instance)`

**Benefits:**
- Session isolation
- State preservation across calls
- Resource cleanup on session end

### Execution Context (AsyncLocal)

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraExecutionContext.cs` (64 lines)

**Pattern:**
```csharp
// ServiceRouter pushes context before service invocation
using (HydraExecutionContext.Push(connection, session, gatewayName))
{
    var result = await service.InvokeAsync(functionName, parameters, context, cancellationToken);
    return result;
}

// Service accesses context
var context = HydraExecutionContext.Current;
var accountId = context?.Connection?.AccountId;
```

**Flows automatically across async/await boundaries via `AsyncLocal<T>`**
