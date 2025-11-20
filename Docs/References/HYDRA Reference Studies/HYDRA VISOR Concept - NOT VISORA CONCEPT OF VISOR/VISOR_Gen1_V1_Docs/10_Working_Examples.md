# 10 - Working Examples from Tests

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

Real VISOR examples extracted from actual test files, showing proven working patterns.

---

## 10.1 Simple Echo (End-to-End Test)

**Source:** `VisorEndToEndTests.cs` (Lines 194-235)

**VISOR Fence:**
```
```VISOR
mcp() {
  { "name": "echo", "message": "ping" }
};
```
```

**Built by Helper:**
```csharp
public static string BuildEchoVisorFence(string message)
{
    var payload = JsonSerializer.Serialize(new
    {
        name = "echo",
        message = message
    });

    return $@"```VISOR
mcp() {{
  {payload}
}};
```";
}
```

**Expected Response:**
```json
{
  "id": "generated-guid",
  "type": "return",
  "op": "mcp",
  "subject": "echo",
  "payload": {
    "result": "Echo: ping",
    "echo": "ping",
    "receivedAt": "2025-11-10T12:00:00Z",
    "envelopeId": "...",
    "status": "success"
  }
}
```

---

## 10.2 With Priority Header

**Source:** `VisorParsingTests.cs` (Lines 52-69)

**Input:**
```javascript
mcp() {
  .headers = { "Priority": 5 };
  { "name": "tools/call", "id": "r1" }
};
```

**Parsed Envelope:**
```csharp
Assert.Single(envelopes);
var envelope = envelopes[0];
Assert.NotNull(envelope.Qos);
Assert.Equal(5, envelope.Qos.Priority);
Assert.Equal("tools/call", envelope.Subject);
```

---

## 10.3 With Metadata (Trace)

**Source:** `VisorParsingTests.cs` (Lines 112-129)

**Input:**
```javascript
mcp() {
  .meta = { "traceparent": "00-1234567890abcdef-0123456789abcdef-01" };
  { "name": "tools/call", "id": "r1" }
};
```

**Assertions:**
```csharp
Assert.Single(envelopes);
var envelope = envelopes[0];
Assert.NotNull(envelope.Trace);
Assert.Equal("00-1234567890abcdef-0123456789abcdef-01", envelope.Trace.TraceParent);
```

---

## 10.4 Trailing Commas (json1s Feature)

**Source:** `VisorParsingTests.cs` (Lines 72-95)

**Input with Trailing Commas:**
```javascript
mcp() {
  {
    "name": "tools/call",
    "id": "r1",
    "arguments": {
      "param1": "value1",  // ← Trailing comma
    },  // ← Trailing comma
  }
};
```

**Result:**
```csharp
Assert.Single(envelopes);
var envelope = envelopes[0];
Assert.Equal("tools/call", envelope.Subject);
Assert.True(envelope.Payload.TryGetProperty("arguments", out var args));
Assert.Equal("value1", args.GetProperty("param1").GetString());
```

**Trailing commas tolerated and parsed correctly!**

---

## 10.5 Multiple Calls in One Fence

**Source:** `VisorParsingTests.cs` (Lines 33-49)

**Input:**
```javascript
mcp() { { "name": "tools/call", "id": "r1" } };
mcp() { { "name": "tools/list", "id": "r2" } };
```

**Result:**
```csharp
Assert.Equal(2, envelopes.Count);
Assert.Equal("tools/call", envelopes[0].Subject);
Assert.Equal("tools/list", envelopes[1].Subject);
```

---

## 10.6 Complex Nested Payload

**Source:** `VisorParsingTests.cs` (Lines 169-203)

**Input:**
```javascript
mcp() {
  .headers = { "Priority": 3 };
  .meta = { "traceparent": "00-abc-def-01" };
  {
    "name": "tools/call",
    "id": "r1",
    "arguments": {
      "nested": {
        "deeply": {
          "value": 42
        }
      }
    }
  }
};
```

**Assertions:**
```csharp
Assert.Single(envelopes);
var envelope = envelopes[0];
Assert.Equal("tools/call", envelope.Subject);
Assert.Equal(3, envelope.Qos?.Priority);
Assert.Equal("00-abc-def-01", envelope.Trace?.TraceParent);

var args = envelope.Payload.GetProperty("arguments");
var nested = args.GetProperty("nested");
var deeply = nested.GetProperty("deeply");
Assert.Equal(42, deeply.GetProperty("value").GetInt32());
```

**Nested structure preserved!**

---

## 10.7 Schema and ContentType

**Source:** `VisorParsingTests.cs` (Lines 146-166)

**Input:**
```javascript
mcp() {
  .meta = {
    "schema": "mcp://schemas/tools/call/1.0",
    "content_type": "application/json"
  };
  { "name": "tools/call", "id": "r1" }
};
```

**Result:**
```csharp
Assert.Equal("mcp://schemas/tools/call/1.0", envelope.Schema);
Assert.Equal("application/json", envelope.ContentType);
```

---

## 10.8 Round-Trip Test (Deterministic Serialization)

**Source:** `VisorEndToEndTests.cs` (Lines 245-330)

**Test:**
```csharp
// Send same message twice
await client.SendTextAsync(visorBlock);
var response1 = await client.ReceiveTextAsync(timeout);

await client.SendTextAsync(visorBlock);
var response2 = await client.ReceiveTextAsync(timeout);

// Parse responses
var envelope1 = JsonSerializer.Deserialize<HydraEnvelope>(response1);
var envelope2 = JsonSerializer.Deserialize<HydraEnvelope>(response2);

// Results should be identical
Assert.Equal(envelope1.Payload.GetProperty("result").GetString(),
             envelope2.Payload.GetProperty("result").GetString());

// Serialize fixed envelope twice
var fixedEnvelope = new HydraEnvelope(...); // Fixed id, ts, etc.
var serialized1 = await deterministicWriter.WriteAsync(fixedEnvelope, options);
var serialized2 = await deterministicWriter.WriteAsync(fixedEnvelope, options);

// Should be byte-identical
Assert.Equal(serialized1, serialized2);
Assert.Equal(Encoding.UTF8.GetBytes(serialized1),
             Encoding.UTF8.GetBytes(serialized2));
```

**Deterministic serialization verified!**

---

## 10.9 Malformed JSON Handling

**Source:** `VisorParsingTests.cs` (Lines 98-109)

**Input (Invalid):**
```javascript
mcp() {
  { "name": "tools/call", "id": "r1" INVALID }
};
```

**Result:**
```csharp
var envelopes = Json1sParser.Parse(prolog, body);
Assert.Empty(envelopes);  // Malformed JSON returns empty list (graceful)
```

**Error logged but parsing continues!**

---

## 10.10 Non-MCP Prolog Rejected

**Source:** `VisorParsingTests.cs` (Lines 132-143)

**Input:**
```javascript
grpc(v="latest", c="protobuf") {
  { "name": "tools/call", "id": "r1" }
};
```

**Result:**
```csharp
var envelopes = Json1sParser.Parse("grpc(...)", body);
Assert.Empty(envelopes);  // Only mcp(...) supported in v1
```

---

## 10.11 Resources List Example

**Source:** `VISOR_v1_Spec.md` (Lines 116-123)

```javascript
mcp() {
  { "name": "resources/list", "id": "r3",
    "arguments": {
      "filter": { "type": "text/markdown" },
      "offset": 0,
      "limit": 10
    }
  }
};
```

---

## 10.12 Prompts Get Example

**Source:** `VISOR_v1_Spec.md` (Lines 128-136)

```javascript
mcp() {
  { "name": "prompts/get", "id": "r4",
    "arguments": {
      "name": "code_review",
      "arguments": { "code": "print('hi')" }
    }
  }
};
```

---

## 10.13 Test File Locations

| Test File | Focus | Key Tests |
|-----------|-------|-----------|
| **VisorParsingTests.cs** | Parser correctness | Happy path, headers, meta, trailing commas |
| **VisorEndToEndTests.cs** | Full round-trip | WebSocket, echo, deterministic output |
| **VBlockPreParserDebugTests.cs** | Fence detection | Regex matching, edge cases |
| **EchoToolTests.cs** | Echo handler | Simple echo, unicode, multiline |
| **PulseSchedulerTests.cs** | Pulse scheduling | Priority, latency, credit |
| **GatewayParsingIntegrationTests.cs** | Gateway parsing | Dual-format support |

---

**See Also:**
- [01 - Protocol Specification](./01_Protocol_Specification.md) - Grammar being tested
- [06 - Parsing Components](./06_Parsing_Components.md) - Parser implementation
- [09 - Service Layer](./09_Service_Layer.md) - Echo handler implementation
