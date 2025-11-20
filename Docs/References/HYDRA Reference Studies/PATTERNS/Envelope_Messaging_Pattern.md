# Envelope Messaging Pattern - Protocol-Agnostic Message Wrapper

**Pattern Category:** Integration / Message Format
**Complexity:** Medium
**Reusability:** Very High - applicable to any multi-protocol messaging system

---

## Pattern Intent

Provide a universal message envelope that:
- Abstracts protocol-specific details (VISOR, HTTP, MCP) into a canonical format
- Supports forward-compatible evolution (add fields without breaking old clients)
- Enables deterministic serialization for testing/reproducibility
- Keeps business logic (services) completely decoupled from transport details

## Problem Being Solved

When building a multi-gateway platform:
- Each protocol has unique message formats (VISOR fenced blocks, HTTP JSON, MCP JSON-RPC)
- Services shouldn't care about transport details
- Adding new fields should not break existing clients/servers
- Testing requires reproducible message serialization
- Distributed tracing, auth, QoS need to flow through all protocols uniformly

## HYDRA Implementation

### Core Envelope Structure

**File:** `Platform/Foundations/Visor Foundation/Envelope/HydraEnvelope.cs` (73 lines)

```csharp
public sealed record HydraEnvelope(
    // Identity & Routing (required)
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] HydraEnvelopeType Type,  // call, return, event, etc.
    [property: JsonPropertyName("ts")] DateTimeOffset Ts,
    [property: JsonPropertyName("src")] string Src,
    [property: JsonPropertyName("dst")] string Dst,

    // Operation Semantics (optional)
    [property: JsonPropertyName("op")] string? Op,              // "mcp", "echo", etc.
    [property: JsonPropertyName("subject")] string? Subject,    // method/topic
    [property: JsonPropertyName("corr")] string? Corr,          // correlation ID

    // Cross-Cutting Concerns (optional)
    [property: JsonPropertyName("trace")] HydraTrace? Trace,    // distributed tracing
    [property: JsonPropertyName("auth")] HydraAuth? Auth,       // authentication proof
    [property: JsonPropertyName("qos")] HydraQos? Qos,          // quality of service
    [property: JsonPropertyName("flow")] HydraFlow? Flow,       // flow control (credits)

    // Payload & Metadata (optional)
    [property: JsonPropertyName("caps")] IReadOnlyList<string>? Caps,       // capabilities
    [property: JsonPropertyName("schema")] string? Schema,                   // payload schema URI
    [property: JsonPropertyName("content_type")] string? ContentType,       // MIME type
    [property: JsonPropertyName("payload")] JsonElement Payload,             // business data
    [property: JsonPropertyName("attachments")] IReadOnlyList<HydraAttachment>? Attachments,
    [property: JsonPropertyName("error")] JsonElement? Error
)
{
    public static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowTrailingCommas = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull  // ← Key: optional fields omitted
    };
}
```

**Envelope Types:**
```csharp
public enum HydraEnvelopeType
{
    call,      // Request to invoke service
    @return,   // Response from service
    @event,    // Unsolicited notification
    sub,       // Subscribe to events
    unsub,     // Unsubscribe from events
    stream,    // Streaming data chunk
    pulse,     // Scheduled/delayed message
    error      // Error response
}
```

**Nested Records for Structure:**
```csharp
// Flow control (credit-based backpressure)
public sealed record HydraFlow(
    [property: JsonPropertyName("credit")] int? Credit
);

// Quality of Service
public sealed record HydraQos(
    [property: JsonPropertyName("priority")] int? Priority,
    [property: JsonPropertyName("ttl")] int? Ttl,
    [property: JsonPropertyName("retries")] int? Retries,
    [property: JsonPropertyName("ordering")] string? Ordering
);

// Distributed tracing (W3C traceparent)
public sealed record HydraTrace(
    [property: JsonPropertyName("traceparent")] string? TraceParent
);

// Authentication proof
public sealed record HydraAuth(
    [property: JsonPropertyName("presenter")] string? Presenter,  // who claims to be sending
    [property: JsonPropertyName("proof")] HydraAuthProof? Proof   // OAuth token, API key, etc.
);
```

---

### Evolvable Design Principles

**Add-Only Contract** (Documented in HYDRA_QUICKSTART_Gen1_V1.md:283-315)

**Rules:**
1. ✅ Can ADD new optional fields with default values
2. ❌ CANNOT remove existing fields
3. ❌ CANNOT change field types
4. ✅ Unknown fields MUST be ignored during deserialization
5. ✅ Old clients read new envelopes (ignore unknown fields)
6. ✅ New clients read old envelopes (missing fields → null)

**Implementation via C# Record + JSON Defaults:**
```csharp
// C# record ensures immutability
public sealed record HydraEnvelope(...)

// JSON serializer ignores null fields (optional fields omitted)
DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull

// Forward compatibility: JsonSerializer tolerates unknown properties by default
```

**Example Evolution:**
```csharp
// v0.1: Original
public sealed record HydraEnvelope(
    string Id,
    HydraEnvelopeType Type,
    JsonElement Payload
    // ... 14 other fields
)

// v0.2: Add new field
public sealed record HydraEnvelope(
    string Id,
    HydraEnvelopeType Type,
    JsonElement Payload,
    // ... 14 other fields,
    [property: JsonPropertyName("new_field")] string? NewField = null  // ← Optional, default null
)

// Old clients: ignore "new_field" in JSON
// New clients: "new_field" is null if missing
// No breaking change!
```

---

### Deterministic Serialization

**Problem:** JSON object key order is undefined. Same envelope can serialize to different byte sequences:
```json
{"id":"1","type":"call"}  vs  {"type":"call","id":"1"}
```

**Solution:** Canonical JSON with lexicographically sorted keys

**File:** `Platform/Core Gateways/VisorGateway/VisorCanonicalizer.cs` (106 lines)

```csharp
public sealed class VisorCanonicalizer
{
    /// <summary>
    /// Canonicalizes JSON by recursively sorting object keys lexicographically.
    /// </summary>
    public JsonElement Canonicalize(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => CanonicalizeObject(element),
            JsonValueKind.Array => CanonicalizeArray(element),
            _ => element.Clone()
        };
    }

    private JsonElement CanonicalizeObject(JsonElement obj)
    {
        // Sort properties by name (Ordinal comparison)
        var sortedProperties = obj.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        // Rebuild JSON with sorted keys
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in sortedProperties)
            {
                writer.WritePropertyName(property.Name);
                Canonicalize(property.Value).WriteTo(writer);  // Recursive
            }
            writer.WriteEndObject();
        }

        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private JsonElement CanonicalizeArray(JsonElement array)
    {
        // Canonicalize elements but preserve array order
        // (arrays are ordered, only objects need key sorting)
    }
}
```

**Usage via IDeterministicWriter:**

**File:** `Platform/Core Gateways/VisorGateway/VisorDeterministicWriter.cs` (89 lines)

```csharp
public sealed class VisorDeterministicWriter : IDeterministicWriter
{
    private readonly VisorCanonicalizer _canonicalizer;

    public async Task<string> WriteAsync(object? value, JsonSerializerOptions? options = null, ...)
    {
        // 1. Serialize to JSON with specified options
        var json = JsonSerializer.Serialize(value, options);

        // 2. Canonicalize to ensure deterministic key ordering
        var canonicalized = _canonicalizer.CanonicalizeString(json);

        return canonicalized;
    }

    public async Task<byte[]> WriteBytesAsync(object? value, ...)
    {
        var json = await WriteAsync(value, options, cancellationToken);
        return Encoding.UTF8.GetBytes(json);  // UTF-8 encoding stable
    }
}
```

**Why This Matters:**
- Testing: Compare byte sequences for equality
- Hashing: Compute SHA256 of canonical JSON for signatures
- Debugging: Reproducible message traces
- Caching: Stable cache keys from message content

---

### Protocol Adapter Pattern: VISOR → Envelope

**Problem:** VISOR protocol has fenced blocks like:
```
```VISOR json1s
op: echo
subject: request
payload:
  {"text": "hello"}
```
```

**Adapter:** Parse VISOR syntax and map to HydraEnvelope

**File:** `Platform/Foundations/Visor Foundation/Parsing/VBlockPreParser.cs` (291 lines)

```csharp
// Extract VISOR fenced blocks from markdown-style ```VISOR ... ```
// Returns list of raw VISOR blocks for further parsing
```

**File:** `Platform/Foundations/Visor Foundation/Parsing/Json1sParser.cs` (388 lines)

```csharp
public class Json1sParser
{
    public List<HydraEnvelope> Parse(string input)
    {
        // Parse prolog (dotted sections like "op:", "subject:")
        var prolog = ParseProlog(input);

        // Parse payload (JSON after prolog)
        var payload = ParsePayload(input);

        // Map to HydraEnvelope
        return new List<HydraEnvelope>
        {
            new HydraEnvelope(
                Id: GenerateId(),
                Type: HydraEnvelopeType.call,
                Ts: DateTimeOffset.UtcNow,
                Src: "client",
                Dst: "server",
                Op: prolog.GetValueOrDefault("op"),
                Subject: prolog.GetValueOrDefault("subject"),
                Payload: JsonSerializer.SerializeToElement(payload),
                // ... other fields default to null (evolvable!)
                Corr: null,
                Trace: null,
                Auth: null,
                Qos: null,
                Flow: null,
                Caps: null,
                Schema: null,
                ContentType: null,
                Attachments: null,
                Error: null
            )
        };
    }
}
```

**Key Insight:** Parser creates envelope with only known fields populated. Unknown VISOR fields ignored (forward compatibility).

---

### Service Layer Isolation: Services Never See Envelopes

**Principle:** Services operate on typed CLR parameters, NOT envelopes

**File:** `Platform/Core Services/VisorHydraService/MessageRouter.cs` (216 lines)

**Unwrapping in MessageRouter:**
```csharp
public async Task<object?> RouteAsync(
    string op,
    string? subject,
    Dictionary<string, object?> parameters,  // ← Unwrapped payload
    CancellationToken cancellationToken)
{
    // Find handler method via reflection
    var method = _handlers[BuildKey(op, subject)];

    // Invoke with unwrapped parameters (NOT envelope)
    var result = method.Invoke(_target, new object[] { parameters, cancellationToken });

    return await ConvertToTask(result);
}
```

**Service Handler Example:**
```csharp
[MessageHandler("echo", "request")]
private async Task<object> HandleEchoRequest(
    Dictionary<string, object?> parameters,  // ← Just the payload!
    CancellationToken cancellationToken)
{
    var text = parameters["text"]?.ToString();
    return new { echo = text };
}
```

**Service Never Sees:**
- Envelope ID, timestamp, src/dst
- Trace headers
- Auth proofs
- QoS settings
- Flow control credits

**Why:** Services are pure business logic. Transport concerns stay in gateway/router layers.

---

## Pattern Structure

### Layered Architecture

```
┌──────────────────────────────────────────────────────────┐
│  Protocol Layer (VISOR, HTTP, MCP)                       │
│  - Protocol-specific syntax                              │
│  - Fenced blocks, JSON-RPC, REST endpoints               │
└────────────┬─────────────────────────────────────────────┘
             │ Adapters (Parsers)
             ↓
┌──────────────────────────────────────────────────────────┐
│  Envelope Layer (HydraEnvelope)                          │
│  - Canonical message format                              │
│  - Evolvable schema (add-only)                           │
│  - Deterministic serialization                           │
│  - Cross-cutting concerns (trace, auth, qos, flow)       │
└────────────┬─────────────────────────────────────────────┘
             │ Unwrapping (MessageRouter)
             ↓
┌──────────────────────────────────────────────────────────┐
│  Service Layer (Business Logic)                          │
│  - Typed CLR parameters                                  │
│  - Transport-agnostic                                    │
│  - No envelope awareness                                 │
└──────────────────────────────────────────────────────────┘
```

### Message Flow: Upstream (Client → Service)

```
1. Client sends VISOR block:
   ```VISOR json1s
   op: mcp
   subject: tools/list
   payload: {}
   ```

2. VBlockPreParser extracts fenced block

3. Json1sParser parses prolog + payload:
   {
     "id": "msg-123",
     "type": "call",
     "ts": "2025-11-10T12:00:00Z",
     "src": "client",
     "dst": "server",
     "op": "mcp",
     "subject": "tools/list",
     "payload": {}
   }

4. MessageRouter unwraps:
   op = "mcp"
   subject = "tools/list"
   parameters = {}

5. Service receives only:
   HandleMcp(parameters, cancellationToken)
```

### Message Flow: Downstream (Service → Client)

```
1. Service returns:
   { "tools": [...] }

2. MessageRouter wraps in envelope:
   {
     "id": "msg-124",
     "type": "return",
     "ts": "2025-11-10T12:00:01Z",
     "src": "server",
     "dst": "client",
     "corr": "msg-123",  ← Correlates to request
     "payload": { "tools": [...] }
   }

3. VisorDeterministicWriter canonicalizes:
   - Sort keys lexicographically
   - Normalize line endings

4. Gateway serializes and sends:
   Deterministic JSON bytes over WebSocket
```

---

## Key Design Decisions

### 1. C# Record for Immutability
**Decision:** Use `sealed record` for envelope

**Rationale:**
- Records provide value-based equality (useful for testing)
- Immutability prevents accidental mutation during message flow
- `with` syntax enables non-destructive updates

**Example:**
```csharp
var request = new HydraEnvelope(...);
var response = request with { Type = HydraEnvelopeType.@return, Payload = result };
// Original request unchanged
```

### 2. JsonElement for Payload (Not object)
**Decision:** `JsonElement Payload` instead of `object? Payload`

**Rationale:**
- Preserves JSON structure without deserialization
- Services deserialize payload to specific types as needed
- Avoids double-serialization (object → JSON → bytes)
- Enables passthrough scenarios (gateway forwards without parsing)

**Tradeoff:** Less strongly-typed, but more flexible

### 3. Omit Null Fields in JSON
**Decision:** `JsonIgnoreCondition.WhenWritingNull`

**Rationale:**
- Smaller message sizes (most fields optional)
- Forward compatibility: new fields absent in old messages
- Backward compatibility: old parsers ignore missing fields

**Example:**
```json
// With nulls (verbose):
{"id":"1","type":"call","op":"echo","subject":null,"corr":null,"trace":null,...}

// Without nulls (concise):
{"id":"1","type":"call","op":"echo"}
```

### 4. Deterministic Serialization (Canonical JSON)
**Decision:** Always sort object keys lexicographically

**Rationale:**
- Reproducible testing (byte equality checks)
- Stable hashing for caching/signatures
- Easier debugging (consistent output format)

**Tradeoff:** ~10% performance overhead from canonicalization, but negligible compared to I/O

### 5. Separation: Envelope vs Service Layer
**Decision:** Services never see envelopes, only unwrapped payloads

**Rationale:**
- Transport-agnostic services (can move to different protocols)
- Easier testing (no envelope mocking)
- Clear separation of concerns

**Enforcement:** MessageRouter unwraps before service invocation

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define Your Envelope Record

```csharp
public sealed record MyEnvelope(
    // Required fields
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,

    // Optional fields (nullable)
    [property: JsonPropertyName("correlation_id")] string? CorrelationId,
    [property: JsonPropertyName("payload")] JsonElement Payload,
    [property: JsonPropertyName("metadata")] Dictionary<string, string>? Metadata
)
{
    public static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,  // or camelCase
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

### Step 2: Implement Canonical JSON Writer

```csharp
public sealed class CanonicalJsonWriter
{
    public string Serialize(object obj)
    {
        // 1. Serialize to JSON
        var json = JsonSerializer.Serialize(obj);

        // 2. Parse and canonicalize
        using var document = JsonDocument.Parse(json);
        var canonicalized = Canonicalize(document.RootElement);

        // 3. Serialize canonical element
        return JsonSerializer.Serialize(canonicalized, new JsonSerializerOptions { WriteIndented = false });
    }

    private JsonElement Canonicalize(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var sorted = element.EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in sorted)
                {
                    writer.WritePropertyName(prop.Name);
                    Canonicalize(prop.Value).WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            stream.Position = 0;
            using var doc = JsonDocument.Parse(stream);
            return doc.RootElement.Clone();
        }

        // Handle arrays, primitives recursively
        return element.Clone();
    }
}
```

### Step 3: Protocol Adapter (Example: HTTP → Envelope)

```csharp
public class HttpToEnvelopeAdapter
{
    public MyEnvelope Adapt(HttpRequest request)
    {
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);

        return new MyEnvelope(
            Id: Guid.NewGuid().ToString(),
            Type: request.Method.ToUpperInvariant(),  // GET, POST, etc.
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: request.Headers["X-Correlation-ID"].FirstOrDefault(),
            Payload: payload,
            Metadata: request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        );
    }
}
```

### Step 4: Unwrap Envelope in Service Layer

```csharp
public class ServiceRouter
{
    public async Task<object> RouteAsync(MyEnvelope envelope)
    {
        // Unwrap envelope
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(envelope.Payload);

        // Invoke service (no envelope knowledge)
        var service = _services[envelope.Type];
        var result = await service.HandleAsync(payload);

        // Wrap result in new envelope
        return new MyEnvelope(
            Id: Guid.NewGuid().ToString(),
            Type: "RESPONSE",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: envelope.Id,  // Correlate to request
            Payload: JsonSerializer.SerializeToElement(result),
            Metadata: null
        );
    }
}
```

### Step 5: Enforce Evolvability

**Rules to communicate to team:**
1. Always add new fields as nullable (`string?`, `int?`, etc.)
2. Never remove existing fields
3. Never change field types
4. Use `DefaultIgnoreCondition.WhenWritingNull`
5. Test backward compatibility (old envelope → new code)
6. Test forward compatibility (new envelope → old code)

**Example Test:**
```csharp
[Fact]
public void OldEnvelope_ParsedByNewCode_IgnoresMissingFields()
{
    var oldJson = """{"id":"1","type":"call","payload":{}}""";  // Missing new "metadata" field

    var envelope = JsonSerializer.Deserialize<MyEnvelope>(oldJson);

    Assert.Equal("1", envelope.Id);
    Assert.Null(envelope.Metadata);  // New field defaults to null
}

[Fact]
public void NewEnvelope_ParsedByOldCode_IgnoresUnknownFields()
{
    var newJson = """{"id":"1","type":"call","payload":{},"new_field":"value"}""";

    // Old code doesn't have "new_field" property, but parsing succeeds
    var envelope = JsonSerializer.Deserialize<MyEnvelope>(newJson);

    Assert.Equal("1", envelope.Id);
    // new_field silently ignored
}
```

---

## Tradeoffs & Constraints

### Advantages
✅ Complete transport decoupling (services agnostic to protocol)
✅ Forward/backward compatible schema evolution
✅ Uniform handling of cross-cutting concerns (auth, trace, qos)
✅ Deterministic serialization for testing/reproducibility
✅ Type-safe with C# records

### Limitations
⚠️ Canonicalization adds ~10% serialization overhead
⚠️ JsonElement less strongly-typed than concrete payload classes
⚠️ Schema evolution requires discipline (no field removal)
⚠️ Large envelopes increase bandwidth (even if most fields null)

### When NOT to Use This Pattern
❌ Single-protocol systems (unnecessary abstraction)
❌ Extreme performance requirements (canonicalization overhead)
❌ Simple request/response (HTTP is envelope enough)
❌ No need for versioning (schema won't evolve)

---

## Related Patterns

- **Adapter Pattern:** Protocol adapters convert external formats to envelopes
- **Mediator Pattern:** Envelope acts as mediator between protocols and services
- **Command Pattern:** Envelope.Type + Envelope.Op identify command to execute
- **Canonical Data Model (EIP):** Envelope is canonical format for multi-protocol integration

---

## Gen2 Evolution Notes

**Current (Gen1):** Envelopes are in-memory CLR objects

**Future (Gen2):**
- Envelopes may be persisted to EventStoreDB event streams
- Akka.NET messages may wrap envelopes
- Wolverine sagas may use envelopes as state

**Migration Strategy:**
- Envelope format remains stable (add-only evolution)
- Serialization layer may change (Akka serialization, protobuf)
- Service layer unaffected (still receives unwrapped payloads)

---

**Last Updated:** 2025-11-10
**Pattern Stability:** Very High - envelope structure is canonical across Gen1/Gen2
**Code References:** HydraEnvelope.cs:1-73, VisorCanonicalizer.cs:1-106, VisorDeterministicWriter.cs:1-89
