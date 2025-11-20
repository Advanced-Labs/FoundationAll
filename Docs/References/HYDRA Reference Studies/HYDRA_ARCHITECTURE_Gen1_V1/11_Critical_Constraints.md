# 11. Critical Constraints & Design Rules

## 11.1 Boundary Rules

### VISOR: Harness-Only, No Policy/Batching

**Rule:** VISOR components (ChatGPTVisor, fence parsers) handle ONLY protocol framing
**Rationale:** Separation of concerns, keep VISOR simple and replaceable

**Allowed:**
- Fence detection (regex)
- Fence emission (string formatting)
- Timing (micro-batch 75ms, linger 200ms)
- State machine (Idle/Generating/Linger)

**Forbidden:**
- Authentication/authorization logic
- Business logic
- Service routing
- Data transformation beyond parsing

**Verification:**
```bash
# ChatGPTVisor should NOT import:
- HydraSecurityService
- ServiceRouter (only gateway uses this)
- Business logic services

# ChatGPTVisor SHOULD import:
- HydraEnvelope (data structure only)
- VisorParsePipeline (parsing only)
```

### Envelope: Stops at Node, Services Never See It

**Rule:** Services receive CLR parameters, not HydraEnvelope objects
**Rationale:** Protocol-agnostic services, testable without envelope knowledge

**Flow:**
```
Gateway
  → Deserializes HydraEnvelope
  → Extracts Payload JsonElement
  → ServiceRouter binds to method parameters
  → Service receives: Dictionary<string, object?>
  → Service returns: object?
  → Gateway wraps in response HydraEnvelope
```

**Service Signature (CORRECT):**
```csharp
[MessageHandler("mcp")]
public async Task<object?> HandleMcp(
    Dictionary<string, object?> parameters,  // ✅ CLR types
    CancellationToken cancellationToken)
```

**Service Signature (WRONG):**
```csharp
[MessageHandler("mcp")]
public async Task<object?> HandleMcp(
    HydraEnvelope envelope,  // ❌ Protocol-specific
    CancellationToken cancellationToken)
```

**Exception:** Services may receive `_envelope` in parameters for metadata access (corr, trace), but should not depend on envelope structure.

### Services: Only CLR Calls, Protocol-Agnostic

**Rule:** Services know nothing about VISOR, MCP, HTTP, or any protocol
**Rationale:** Same service callable from any gateway, testable in isolation

**Service Dependencies (ALLOWED):**
- Other services (via DI)
- Repositories (IHydraStoreSystem)
- Utilities (logging, config)
- [InjectCaller*] fields (connection, session, account)

**Service Dependencies (FORBIDDEN):**
- Gateways (VisorGateway, MCP Gateway)
- Protocol-specific types (VISOR fences, MCP schemas)
- HTTP context (HttpContext)

**Test:**
```csharp
// Services should be testable without gateway:
var service = new VisorHydraService();
var result = await service.InvokeAsync("ProcessEnvelopes", new Dictionary<string, object?> {
    ["envelopes"] = new[] { CreateTestEnvelope() }
}, context, cancellationToken);

// No gateway, no VISOR, just CLR calls
```

## 11.2 Evolvability Guarantees

### Envelope v0.2 is Add-Only

**Rule:** Never remove or rename HydraEnvelope fields
**Rationale:** Forward/backward compatibility between client/server versions

**Allowed:**
- Add new optional fields (nullable)
- Add new enum values (with Unknown fallback)
- Add new supporting types (e.g., HydraQos, HydraTrace)

**Forbidden:**
- Remove existing fields
- Rename fields (breaks JSON deserialization)
- Change field types (string → int)
- Make optional fields required

**Example (CORRECT):**
```csharp
// Envelope v0.2
record HydraEnvelope(
    string Id,
    HydraEnvelopeType Type,
    // ... existing fields ...
    string? NewField  // ✅ Optional, add-only
);
```

**Example (WRONG):**
```csharp
// Envelope v0.3 (BREAKING)
record HydraEnvelope(
    string Id,
    HydraEnvelopeType Type,
    // string Op,  ❌ Removed field (breaks old clients)
    string Operation,  // ❌ Renamed (breaks JSON mapping)
    int Priority  // ❌ Changed from Qos.Priority (breaks structure)
);
```

### Unknown Fields Ignored by Services

**Rule:** Parsers and services MUST ignore unknown JSON fields
**Rationale:** Allows new clients to add fields without breaking old servers

**Implementation:**
```csharp
// HydraEnvelope.JsonOptions
JsonSerializerOptions {
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip  // ✅ Ignore unknown
}
```

**Test:**
```csharp
// Old server receives envelope from new client:
{
  "id": "123",
  "type": "call",
  "op": "mcp",
  "newFieldFromV0_3": "value",  // ← Old server ignores this
  "payload": { ... }
}

// Deserialize succeeds, newFieldFromV0_3 silently ignored
```

### JSON Schema Validation at Gateway

**Rule:** Gateways validate envelopes against schema before routing
**Rationale:** Reject malformed envelopes early, protect services

**Schema File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Envelope/EnvelopeSchema.json`

**Validation:**
```csharp
// VisorGateway (future implementation)
var isValid = JsonSchemaValidator.Validate(envelopeJson, EnvelopeSchema);
if (!isValid) {
    SendError(connectionId, "Invalid envelope format");
    return;
}
```

**Current Status:** ⚠️ Schema exists but validation not enforced (Gen1 V1)

## 11.3 OAuth Invariants (MUST NOT BREAK)

### Invariant 1: `hydra.mcp` Scope Only

**Rule:** All OAuth tokens have implicit `hydra.mcp` scope, no other scopes allowed
**Enforcement:** Scope stripping middleware (OAuth.cs Lines 34-150)
**Rationale:** Simplifies token validation in dev mode, prevents scope proliferation

**Verification:**
```bash
# Token request (client sends):
POST /connect/token
scope=openid profile email

# Middleware strips scope parameter

# Token issued (no scope claim):
{
  "sub": "hydra",
  "aud": "http://localhost:5080/mcp",
  # No "scope" claim
}
```

**MUST NOT:**
- Add scope validation to OpenIddict config
- Allow scope parameter in token requests
- Issue tokens with scope claims

**Breaking This:** MCP Gateway token validation will fail

### Invariant 2: Token-Exchange Scope Stripping

**Rule:** Authorization code token requests have scope parameter removed
**Enforcement:** Middleware + OpenIddict event handler
**Rationale:** Workaround for OpenIddict scope validation issue

**Middleware (OAuth.cs Lines 34-150):**
```csharp
if (context.Request.Method == "POST" && context.Request.Path == "/connect/token") {
    // Remove scope parameter from body
}
```

**Event Handler (ConfigureServices.cs Lines 75-89):**
```csharp
.AddEventHandler<ExtractTokenRequestContext>(builder => {
    builder.UseInlineHandler(context => {
        if (context.Request.GrantType == GrantTypes.AuthorizationCode) {
            context.Request.Scope = null;  // Strip scope
        }
    });
});
```

**MUST NOT:**
- Remove scope stripping
- Add scope validation
- Change grant type filtering

**Breaking This:** Token exchange flow breaks

### Invariant 3: `aud` Fallback for MCP Tokens

**Rule:** If no `resource` parameter, default to `http://localhost:5080/mcp`
**Enforcement:** `ResolveDefaultResource()` (OAuth.cs Lines 538-542)
**Rationale:** Ensures MCP Gateway can validate tokens

**Fallback Logic:**
```csharp
private async Task<string?> ResolveDefaultResource()
{
    var resources = await _resourceRegistry.ListAsync();
    return resources.FirstOrDefault()?.Url ?? "http://localhost:5080/mcp";
}
```

**MCP Gateway Validation (HydraMcpSdkHost.cs Line 136):**
```csharp
options.AddAudiences("http://localhost:5080/mcp", "http://127.0.0.1:5080/mcp");
```

**MUST NOT:**
- Change default resource URL
- Require resource parameter
- Remove audience fallback

**Breaking This:** MCP clients can't connect (401 Unauthorized)

## 11.4 Threading & Concurrency Rules

### AsyncLocal Context Flow

**Rule:** Use `HydraExecutionContext.Push()` pattern, never set `_current.Value` directly
**Rationale:** Ensures context is properly restored, prevents leaks

**CORRECT:**
```csharp
using (HydraExecutionContext.Push(connection, session, gateway))
{
    await DoWorkAsync();  // Context flows here
}
// Context automatically restored
```

**WRONG:**
```csharp
HydraExecutionContext._current.Value = new HydraExecutionContext(...);  // ❌ No restore
await DoWorkAsync();
// Context leaked!
```

### Credit-Based Flow Control

**Rule:** `VisorConnectionQueue.TryDequeue()` MUST check credit before dequeue
**Rationale:** Prevents overwhelming slow clients

**Enforcement (VisorConnectionQueue.cs Lines 80-98):**
```csharp
public bool TryDequeue(out HydraEnvelope? envelope)
{
    lock (_creditLock)
    {
        if (_credit <= 0 || _queue.IsEmpty) {  // ✅ Credit check
            envelope = null;
            return false;
        }
        
        if (_queue.TryDequeue(out envelope)) {
            _credit--;  // ✅ Consume credit
            return true;
        }
    }
}
```

**MUST NOT:**
- Dequeue without credit check
- Allow negative credit
- Bypass credit system

### ConcurrentDictionary for Shared State

**Rule:** Use `ConcurrentDictionary` for thread-safe caches, not `Dictionary` + lock
**Rationale:** Better performance, built-in thread safety

**Examples:**
- `HydraServiceActivator._injectionCache` (Line 19)
- `VisorGateway._connectionQueues` (Line 40)
- `HydraServiceBase._instanceRegistry` (Line 28)

## 11.5 Performance Constraints

### Reflection Caching Required

**Rule:** Reflection scans MUST be cached per type, not per instance
**Rationale:** Reflection is expensive (microseconds per scan)

**CORRECT (HydraServiceActivator):**
```csharp
_injectionCache.GetOrAdd(serviceType, BuildInjectionDescriptor);  // ✅ Cached
```

**WRONG:**
```csharp
BuildInjectionDescriptor(serviceType);  // ❌ Re-scan every instance
```

### Deterministic JSON Serialization

**Rule:** Use `VisorDeterministicWriter` for gateway responses
**Rationale:** Enables byte-identical caching, content addressing

**Implementation (VisorDeterministicWriter.cs):**
- Sort object properties alphabetically
- Consistent number formatting
- Stable enum serialization

**Usage (VisorGateway.cs Line 354):**
```csharp
var json = await _deterministicWriter.WriteAsync(envelope, HydraEnvelope.JsonOptions, cancellationToken);
```

## 11.6 Error Handling Rules

### Envelope Error Field

**Rule:** Services return errors as `HydraEnvelope.Error` JsonElement, not exceptions
**Rationale:** Protocol-level errors, client can parse

**CORRECT:**
```csharp
return new HydraEnvelope(
    Type: HydraEnvelopeType.error,
    Error: JsonSerializer.SerializeToElement(new {
        code = "INVALID_PARAMS",
        message = "Missing required parameter 'name'"
    })
);
```

**WRONG:**
```csharp
throw new ArgumentException("Missing parameter");  // ❌ Exception not catchable by client
```

### Circuit Breaker for External Calls

**Rule:** Wrap external service calls (MCP servers) in circuit breaker
**Rationale:** Prevent cascading failures

**Status:** ⚠️ Planned, not yet implemented
