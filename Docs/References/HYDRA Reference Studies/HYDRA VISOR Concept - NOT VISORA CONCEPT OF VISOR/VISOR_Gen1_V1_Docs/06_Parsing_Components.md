# 06 - Parsing Components Deep Dive

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

This document details the three core parsing components that convert VISOR fenced blocks into HydraEnvelopes.

**Pipeline:**
```
Raw Message → VBlockPreParser → Json1sParser → HydraEnvelopes
```

---

## 6.1 VBlockPreParser

**File:** `Platform/Foundations/Visor Foundation/Parsing/VBlockPreParser.cs`
**Lines:** 291
**Status:** ✅ Implemented

### Purpose

Extract ```VISOR fenced blocks from raw chat messages and split into (prolog, body) pairs.

### Data Structure

```csharp
public record VBlock(string Prolog, string Body);
```

### Main Method

**`Extract(string message)`** → `IReadOnlyList<VBlock>` (Lines 24-66)

```csharp
public static IReadOnlyList<VBlock> Extract(string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return Array.Empty<VBlock>();
    }

    var blocks = new List<VBlock>();

    // Extract all ```VISOR...``` fenced blocks
    var visorBlocks = ExtractVisorFencedBlocks(message);

    // Parse each VISOR block to extract V-calls
    foreach (var visorBlock in visorBlocks)
    {
        var vCalls = ExtractVCalls(visorBlock);
        blocks.AddRange(vCalls);
    }

    return blocks.AsReadOnly();
}
```

### Algorithm Steps

**Step 1: Extract Fenced Blocks** (Lines 71-90)

```csharp
var pattern = @"```\s*VISOR\s*[\r\n]+(.*?)```";
var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
```

**Regex Breakdown:**
- ` ``` ` - Three backticks
- `\s*` - Optional whitespace
- `VISOR` - Label (case-insensitive)
- `\s*[\r\n]+` - Whitespace + newline (Unix or Windows)
- `(.*?)` - Content (non-greedy capture)
- ` ``` ` - Closing backticks

**Step 2: Extract V-Calls** (Lines 96-172)

Parses `protoName(...) { ... };` patterns:

```csharp
while (pos < visorBlock.Length)
{
    // Skip whitespace
    // Find prolog: identifier(...)
    var prologEnd = FindPrologEnd(visorBlock, pos);

    // Extract prolog
    var prolog = visorBlock.Substring(prologStart, prologEnd - prologStart);

    // Find body: { ... }
    var bodyEnd = FindBalancedBraceEnd(visorBlock, pos);

    // Extract body (excluding outer braces)
    var body = visorBlock.Substring(bodyStart + 1, bodyEnd - bodyStart - 2);

    vCalls.Add(new VBlock(prolog.Trim(), body.Trim()));

    // Skip optional semicolon
}
```

### Prolog Parsing (Lines 178-241)

**`FindPrologEnd(string text, int start)`** → `int`

**Algorithm:**
1. Read identifier: `[a-zA-Z_][a-zA-Z0-9_]*`
2. Expect `(`
3. Find matching `)` with balanced paren tracking
4. Handle string escapes (ignore parens in strings)
5. Return position after closing `)`

**Example:**
```
mcp(v="latest", c="json1s")
    ^                       ^
  start                   return
```

### Body Parsing (Lines 247-289)

**`FindBalancedBraceEnd(string text, int start)`** → `int`

**Algorithm:**
1. Expect `{` at start
2. Track brace depth
3. Ignore braces inside strings
4. Handle escape sequences
5. Return position after closing `}`

**Example:**
```
{ .headers = { "Priority": 5 }; { "name": "echo" } }
^                                                    ^
start                                              return
```

### Edge Cases Handled

| Case | Handling |
|------|----------|
| **Nested braces** | Balanced tracking |
| **Escaped quotes in strings** | `prevChar != '\\'` check |
| **Multiple V-calls** | Loop continues until end |
| **Malformed prolog** | Return -1, skip block |
| **Unbalanced braces** | Return -1, skip block |
| **Whitespace variations** | Trim and skip whitespace |

---

## 6.2 Json1sParser

**File:** `Platform/Foundations/Visor Foundation/Parsing/Json1sParser.cs`
**Lines:** 388
**Status:** ✅ Implemented

### Purpose

Parse MCP-format V-calls into HydraEnvelopes

### Main Method

**`Parse(string prolog, string body)`** → `IReadOnlyList<HydraEnvelope>` (Lines 26-71)

```csharp
public static IReadOnlyList<HydraEnvelope> Parse(string prolog, string body)
{
    // Only accept mcp(...) prolog for MVP
    if (!prolog.TrimStart().StartsWith("mcp(", StringComparison.OrdinalIgnoreCase))
    {
        return Array.Empty<HydraEnvelope>();
    }

    // Split body by semicolon to handle multiple V-calls
    var vCalls = SplitVCalls(body);

    var envelopes = new List<HydraEnvelope>();

    foreach (var vCallBody in vCalls)
    {
        var envelope = ParseSingleVCall(vCallBody);
        if (envelope != null)
        {
            envelopes.Add(envelope);
        }
    }

    return envelopes.AsReadOnly();
}
```

### Algorithm Steps

**Step 1: Validate Prolog** (Lines 33-38)

Only `mcp(...)` supported in v1:
```csharp
if (!prolog.TrimStart().StartsWith("mcp(", StringComparison.OrdinalIgnoreCase))
{
    return Array.Empty<HydraEnvelope>();
}
```

**Step 2: Split V-Calls** (Lines 73-150)

**Problem:** Multiple payloads separated by `;` but dotted sections also use `;`

**Solution:** Smart splitting with state tracking:

```csharp
// Track:
// - Brace depth (only split at depth 0)
// - String context (ignore ; in strings)
// - Dotted section context (don't split between .headers and payload)

if (c == ';' && braceDepth == 0)
{
    // Check if next is '{' (could be payload) or '.' (dotted section)
    // Only split if appropriate
}
```

**Step 3: Parse Single V-Call** (Lines 152-252)

1. Extract dotted sections (`.headers`, `.meta`)
2. Parse remaining body as JSON payload
3. Map to HydraEnvelope fields

### Dotted Section Extraction (Lines 260-386)

**`ExtractDottedSectionsAndPayload(string vCallBody)`** → `(Dictionary<string, string> sections, string remainingBody)`

**State Machine:**

```
Start → Skip whitespace → Check for '.'
        ├─ No '.' → remainingBody is entire content
        └─ Yes '.' → Extract dotted section
                     ├─ Read identifier after '.'
                     ├─ Expect '='
                     ├─ Extract balanced { ... }
                     ├─ Expect ';'
                     └─ Loop back to check for next '.'
```

**Example:**
```javascript
Input:
  .headers = { "Priority": 5 };
  .meta = { "traceparent": "00-abc-def-01" };
  { "name": "echo" }

Output:
  sections = {
    "headers": "{ \"Priority\": 5 }",
    "meta": "{ \"traceparent\": \"00-abc-def-01\" }"
  }
  remainingBody = "{ \"name\": \"echo\" }"
```

### VISOR → Envelope Mapping (Lines 174-244)

```csharp
// Extract values from dotted sections
int? priority = null;
string? traceparent = null;

if (sections.TryGetValue("headers", out var headersJson))
{
    using var headers = JsonDocument.Parse(headersJson);
    if (headers.RootElement.TryGetProperty("Priority", out var priorityProp))
    {
        priority = priorityProp.GetInt32();
    }
}

if (sections.TryGetValue("meta", out var metaJson))
{
    using var meta = JsonDocument.Parse(metaJson);
    if (meta.RootElement.TryGetProperty("traceparent", out var traceparentProp))
    {
        traceparent = traceparentProp.GetString();
    }
}

// Parse payload
var payload = JsonDocument.Parse(remainingBody);
var subject = payload.RootElement.TryGetProperty("name", out var nameProp)
    ? nameProp.GetString()
    : null;

// Create envelope
var envelope = new HydraEnvelope(
    Id: Guid.NewGuid().ToString("D"),
    Type: HydraEnvelopeType.call,
    Op: "mcp",
    Subject: subject,
    Qos: priority != null ? new HydraQos(priority, null, null, null) : null,
    Trace: traceparent != null ? new HydraTrace(traceparent) : null,
    Payload: payload.RootElement.Clone()
);
```

### JSON Options (Lines 15-19)

```csharp
private static readonly JsonDocumentOptions JsonOptions = new JsonDocumentOptions
{
    AllowTrailingCommas = true,          // json1s feature
    CommentHandling = JsonCommentHandling.Skip
};
```

**Trailing Comma Example:**
```javascript
{
  "name": "echo",
  "message": "ping",  // ← Trailing comma allowed
}
```

---

## 6.3 VisorParsePipeline

**File:** `Platform/Foundations/Visor Foundation/Parsing/VisorParsePipeline.cs`
**Lines:** 84
**Status:** ✅ Implemented

### Purpose

Orchestrate VBlockPreParser + Json1sParser (thin façade)

### Main Method

**`ExtractAndParse(string message)`** → `IReadOnlyList<HydraEnvelope>` (Lines 21-82)

```csharp
public static IReadOnlyList<HydraEnvelope> ExtractAndParse(string message)
{
    if (string.IsNullOrWhiteSpace(message))
    {
        return Array.Empty<HydraEnvelope>();
    }

    // Step 1: Extract VISOR blocks (prolog, body pairs)
    var vBlocks = VBlockPreParser.Extract(message);

    if (vBlocks.Count == 0)
    {
        return Array.Empty<HydraEnvelope>();
    }

    // Step 2: Parse each block to produce envelopes
    var allEnvelopes = new List<HydraEnvelope>();

    foreach (var vBlock in vBlocks)
    {
        try
        {
            var envelopes = Json1sParser.Parse(vBlock.Prolog, vBlock.Body);

            if (envelopes.Count > 0)
            {
                allEnvelopes.AddRange(envelopes);
            }
        }
        catch (Exception ex)
        {
            // Ignore malformed blocks, continue processing others
            Logger.LogError(ex, "Failed to parse V-block");
        }
    }

    return allEnvelopes.AsReadOnly();
}
```

### Pipeline Diagram

```
┌─────────────────────────────────────────────────────────┐
│  Input: Raw message with VISOR fences                   │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│  VBlockPreParser.Extract(message)                       │
│  - Regex: ```VISOR ... ```                              │
│  - Extract content between fences                        │
│  - Parse: protoName(...) { ... };                       │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│  List<VBlock>                                            │
│  VBlock(prolog="mcp()", body=".headers={...}; {...}")  │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼ For each VBlock
┌─────────────────────────────────────────────────────────┐
│  Json1sParser.Parse(prolog, body)                       │
│  - Validate prolog (must be "mcp(...)")                 │
│  - Extract dotted sections (.headers, .meta)            │
│  - Parse payload JSON                                    │
│  - Map to HydraEnvelope                                 │
└────────────────┬────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────┐
│  List<HydraEnvelope>                                     │
│  Ready for routing to services                           │
└─────────────────────────────────────────────────────────┘
```

### Error Handling

- **Malformed blocks:** Logged, skipped (doesn't stop processing)
- **Empty results:** Tolerated (returns empty list)
- **Exceptions:** Caught per-block (one bad block doesn't break others)
- **Graceful degradation:** Best effort parsing

### Used By

| Component | File | Line | Purpose |
|-----------|------|------|---------|
| **ChatGPTVisor** | ChatGPTVisor.cs | 374 | Parse LLM output |
| **VisorGateway** | VisorGateway.cs | 276 | Parse incoming VISOR messages |

---

## 6.4 Parsing Flow Examples

### Example 1: Simple Echo

**Input:**
```
```VISOR
mcp() {
  { "name": "echo", "message": "ping" }
};
```
```

**VBlockPreParser Output:**
```csharp
List<VBlock> {
    new VBlock(
        Prolog: "mcp()",
        Body: "{ \"name\": \"echo\", \"message\": \"ping\" }"
    )
}
```

**Json1sParser Output:**
```json
[
  {
    "id": "generated-guid",
    "type": "call",
    "op": "mcp",
    "subject": "echo",
    "payload": {
      "name": "echo",
      "message": "ping"
    }
  }
]
```

### Example 2: With Headers and Meta

**Input:**
```
```VISOR
mcp() {
  .headers = { "Priority": 5 };
  .meta = { "traceparent": "00-abc-def-01" };
  { "name": "tools/call", "id": "r1" }
};
```
```

**VBlockPreParser Output:**
```csharp
List<VBlock> {
    new VBlock(
        Prolog: "mcp()",
        Body: ".headers = { \"Priority\": 5 };\n.meta = { \"traceparent\": \"00-abc-def-01\" };\n{ \"name\": \"tools/call\", \"id\": \"r1\" }"
    )
}
```

**Json1sParser Output:**
```json
[
  {
    "id": "generated-guid",
    "type": "call",
    "op": "mcp",
    "subject": "tools/call",
    "qos": {
      "priority": 5
    },
    "trace": {
      "traceparent": "00-abc-def-01"
    },
    "payload": {
      "name": "tools/call",
      "id": "r1"
    }
  }
]
```

### Example 3: Multiple Calls

**Input:**
```
```VISOR
mcp() { { "name": "echo", "message": "first" } };
mcp() { { "name": "echo", "message": "second" } };
```
```

**VBlockPreParser Output:**
```csharp
List<VBlock> {
    new VBlock(Prolog: "mcp()", Body: "{ \"name\": \"echo\", \"message\": \"first\" }"),
    new VBlock(Prolog: "mcp()", Body: "{ \"name\": \"echo\", \"message\": \"second\" }")
}
```

**Json1sParser Output:**
```json
[
  {
    "id": "guid-1",
    "subject": "echo",
    "payload": { "name": "echo", "message": "first" }
  },
  {
    "id": "guid-2",
    "subject": "echo",
    "payload": { "name": "echo", "message": "second" }
  }
]
```

---

## 6.5 Performance Characteristics

| Component | Operation | Complexity | Notes |
|-----------|-----------|------------|-------|
| **VBlockPreParser** | Regex fence detection | O(n) | Single pass, compiled regex |
| **VBlockPreParser** | Prolog parsing | O(n) | Linear scan with brace tracking |
| **VBlockPreParser** | Body extraction | O(n) | Balanced brace matching |
| **Json1sParser** | Dotted section extraction | O(n) | Sequential state machine |
| **Json1sParser** | JSON parsing | O(n) | System.Text.Json (fast) |
| **VisorParsePipeline** | Orchestration | O(n) | Aggregates results |

**Overall:** O(n) where n = message length

**Bottlenecks:**
- Regex matching (minimized with compiled regex)
- JSON parsing (System.Text.Json is optimized)
- Multiple string allocations (could be optimized with Span<char>)

---

## 6.6 Known Limitations

### VBlockPreParser
- No support for nested fences (fence inside fence)
- No validation of prolog syntax (just extracts)
- Large messages (>100KB) might trim buffer mid-fence

### Json1sParser
- Only `mcp()` prolog supported (others rejected)
- Unknown dotted sections silently ignored
- No schema validation on payload
- Trailing comma support might not match all JSON parsers

### VisorParsePipeline
- No caching of parsed results
- No parallel parsing of multiple blocks
- No streaming parser (must have full message)

---

**See Also:**
- [01 - Protocol Specification](./01_Protocol_Specification.md) - Grammar being parsed
- [04 - ChatGPTVisor](./04_ChatGPTVisor.md) - Client-side usage
- [05 - VisorGateway](./05_VisorGateway.md) - Server-side usage
- [10 - Working Examples](./10_Working_Examples.md) - Real parsing examples from tests
