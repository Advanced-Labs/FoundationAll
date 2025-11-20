# 09 - Service Layer (VisorHydraService & MessageRouter)

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

The service layer routes parsed VISOR envelopes to appropriate handlers using reflection-based dispatch.

---

## 9.1 VisorHydraService

**File:** `Platform/Core Services/VisorHydraService/VisorHydraService.cs`
**Lines:** 304
**Status:** ✅ Operational

### Purpose

Visor-aware Hydra service that:
1. Receives parsed envelopes from VisorGateway
2. Routes by `op/subject` to handlers
3. Processes MCP protocol calls
4. Returns results as envelopes

### Architecture

```
┌────────────────────────────────────────────┐
│       VisorHydraService                    │
├────────────────────────────────────────────┤
│                                             │
│  ProcessEnvelopes(List<HydraEnvelope>)    │
│          │                                  │
│          ▼                                  │
│  ┌─────────────────────┐                  │
│  │  MessageRouter      │                  │
│  │  Reflection-based   │                  │
│  └─────────────────────┘                  │
│          │                                  │
│          ├──▶ [MessageHandler("mcp")]      │
│          │    HandleMcp()                   │
│          │                                  │
│          └──▶ HandleDefault()               │
│               (fallback)                    │
│                                             │
└────────────────────────────────────────────┘
```

### Main Method

**ProcessEnvelopes** (Lines 60-109):

```csharp
private async Task<object?> ProcessEnvelopes(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    var envelopes = ExtractEnvelopesFromParameters(parameters);
    var results = new List<object?>();

    foreach (var envelope in envelopes)
    {
        try
        {
            var result = await ProcessSingleEnvelope(envelope, cancellationToken);
            results.Add(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Failed to process envelope {envelope.Id}");
            results.Add(new { error = ex.Message, envelopeId = envelope.Id });
        }
    }

    return results;
}
```

### ProcessSingleEnvelope (Lines 111-143)

```csharp
private async Task<object?> ProcessSingleEnvelope(
    HydraEnvelope envelope,
    CancellationToken cancellationToken)
{
    var op = envelope.Op ?? "unknown";
    var subject = envelope.Subject;

    // Extract parameters from payload
    var parameters = new Dictionary<string, object?>();
    foreach (var prop in envelope.Payload.EnumerateObject())
    {
        parameters[prop.Name] = prop.Value;
    }
    parameters["_envelope"] = envelope;  // Add metadata

    // Route based on op/subject
    if (_router.HasHandler(op, subject))
    {
        return await _router.RouteAsync(op, subject, parameters, cancellationToken);
    }

    // Fallback
    return await HandleDefault(envelope, cancellationToken);
}
```

---

## 9.2 MessageRouter

**File:** `Platform/Core Services/VisorHydraService/MessageRouter.cs`
**Lines:** 216
**Status:** ✅ Operational

### Purpose

Reflection-based router that:
1. Discovers handlers via `[MessageHandler]` attributes
2. Routes messages to appropriate methods
3. Handles async method invocation
4. Provides fallback routing

### Handler Discovery (Lines 155-175)

```csharp
private void DiscoverHandlers()
{
    var type = _target.GetType();
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    foreach (var method in methods)
    {
        var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
        if (attr != null)
        {
            var key = BuildKey(attr.Op, attr.Subject);
            _handlers[key] = method;

            Logger.LogDebug(
                $"Registered handler: {method.Name} for op={attr.Op}, subject={attr.Subject ?? "(any)"}");
        }
    }
}
```

### Routing Logic (Lines 47-128)

```csharp
public async Task<object?> RouteAsync(
    string op,
    string? subject,
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    var key = BuildKey(op, subject);  // "op:subject" or just "op"

    // Try exact match
    if (!_handlers.TryGetValue(key, out var method))
    {
        // Fallback: if subject provided but no exact match, try op-only
        if (!string.IsNullOrEmpty(subject) && _handlers.TryGetValue(op, out method))
        {
            Logger.LogDebug($"Using fallback op-only handler for op={op}, subject={subject}");
        }
        else
        {
            throw new InvalidOperationException($"No handler for op='{op}', subject='{subject}'");
        }
    }

    // Prepare method parameters
    var args = PrepareArguments(method, parameters, cancellationToken);

    // Invoke
    var result = method.Invoke(_target, args);

    // Handle async
    if (result is Task task)
    {
        await task;
        return ExtractTaskResult(task);
    }

    return result;
}
```

### Key Building (Lines 177-180)

```csharp
private static string BuildKey(string op, string? subject)
{
    return subject == null ? op : $"{op}:{subject}";
}
```

**Examples:**
- `("mcp", null)` → `"mcp"`
- `("mcp", "tools/call")` → `"mcp:tools/call"`

---

## 9.3 Message Handler Attribute

**Definition** (Lines 204-215):

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MessageHandlerAttribute : Attribute
{
    public string Op { get; }
    public string? Subject { get; }

    public MessageHandlerAttribute(string op, string? subject = null)
    {
        Op = op ?? throw new ArgumentNullException(nameof(op));
        Subject = subject;
    }
}
```

**Usage:**

```csharp
[MessageHandler("mcp")]
private async Task<object?> HandleMcp(Dictionary<string, object?> parameters, CancellationToken ct)
{
    // Handle MCP protocol calls
}

[MessageHandler("echo", "ping")]
private Task<object?> HandleEchoPing(Dictionary<string, object?> parameters, CancellationToken ct)
{
    // Handle specific echo ping
}
```

---

## 9.4 Current Handlers

### HandleMcp (Lines 149-263)

**Attribute:** `[MessageHandler("mcp")]`

**Purpose:** Route MCP protocol calls

**Logic:**
1. Extract `_envelope` from parameters
2. Try to extract `method` from `payload.method` or `payload.name`
3. Extract `params` from `payload.params` or entire payload
4. Route based on method name

**Echo Implementation** (Lines 228-251):

```csharp
if (method == "echo")
{
    // Extract message from params
    string message = "No message provided";
    if (mcpParams != null && mcpParams.TryGetValue("message", out var msgObj))
    {
        message = msgObj?.ToString() ?? "No message provided";
    }
    else if (mcpParams != null && mcpParams.TryGetValue("text", out var textObj))
    {
        message = textObj?.ToString() ?? "No message provided";
    }

    result = new
    {
        echo = message,
        receivedAt = DateTimeOffset.UtcNow.ToString("o"),
        envelopeId = envelope.Id,
        method = method,
        status = "success"
    };
}
else
{
    throw new NotSupportedException($"MCP method '{method}' not supported");
}
```

### HandleDefault (Lines 268-279)

**Fallback for unrecognized messages:**

```csharp
private Task<object?> HandleDefault(HydraEnvelope envelope, CancellationToken ct)
{
    Logger.LogWarning($"No specific handler for envelope {envelope.Id}, op={envelope.Op}");

    return Task.FromResult<object?>(new
    {
        status = "unhandled",
        envelopeId = envelope.Id,
        op = envelope.Op,
        subject = envelope.Subject
    });
}
```

---

## 9.5 Integration with VisorGateway

**VisorGateway raises event** (Lines 296-302):

```csharp
EnvelopesReceived?.Invoke(this, new EnvelopesReceivedEventArgs
{
    ConnectionId = connectionId,
    Envelopes = envelopes
});
```

**Test harness subscribes** (VisorEndToEndTests.cs:68):

```csharp
_gateway.EnvelopesReceived += OnEnvelopesReceived;
```

**Handler calls VisorHydraService** (VisorEndToEndTests.cs:110):

```csharp
var result = await _visorHydraService.InvokeAsync(
    "ProcessEnvelopes",
    new Dictionary<string, object?> { ["envelopes"] = envelopes },
    context,
    cancellationToken);
```

---

## 9.6 Known Limitations

### MessageRouter is Temporary
- Reflection-based (performance overhead)
- No caching of method info
- Discovery happens per instance
- Planned replacement with code-gen router

### Handler Registration
- Manual attribute decoration required
- No automatic discovery across assemblies
- No dynamic registration at runtime
- No unregistration support

### Echo Implementation
- Hardcoded in HandleMcp (not extensible)
- No actual MCP server integration (MVP only)
- Testing mode only

### Error Handling
- Exceptions caught per-envelope (doesn't stop batch)
- No retry logic
- No circuit breaker
- Errors logged but connection stays open

---

## 9.7 Future Enhancements

**Code-gen Router:**
- Source generators for zero-reflection routing
- Compile-time handler discovery
- Better performance

**Handler Registry:**
- Dynamic handler registration
- Plugin architecture
- Hot-reload support

**MCP Server Integration:**
- Route to actual MCP server (XmcpClientHydraService)
- Support all MCP methods
- Handle streaming responses

**Metrics:**
- Handler execution time
- Success/failure rates
- Throughput monitoring

---

**See Also:**
- [05 - VisorGateway](./05_VisorGateway.md) - EnvelopesReceived event
- [10 - Working Examples](./10_Working_Examples.md) - Echo handler examples
- [11 - Evolution Roadmap](./11_Evolution_Roadmap.md) - Router replacement plans
