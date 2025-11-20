# 01 - VISOR Protocol Specification (Canonical)

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

This document defines the canonical VISOR v1 protocol specification including grammar, framing formats, and payload rules.

---

## 1.1 What VISOR Is (and Isn't)

**VISOR is:**
- A **harness-level framing grammar** for embedding protocol-native payloads in LLM chat streams
- A **carrier** for MCP v1 protocol calls (v1 implementation)
- A **frame detection** mechanism only

**VISOR is NOT:**
- A transport layer (not a queue, not a protocol engine)
- A batching system (that's server-side)
- A policy enforcement mechanism
- A security layer

**Responsibility Boundary:** Frame detection and extraction only. All higher-level concerns (auth, caps, batching, flow control) belong server-side in Hydra Node.

**File Reference:** `/Sprints/8 - VISOR ZERO/VISOR_v1_Spec.md`

---

## 1.2 Web Framing (Triple-Backtick Fences)

VISOR blocks in web chat interfaces use triple-backtick markdown fences labeled `VISOR`:

```
```VISOR
mcp() {
  .headers = { "Priority": 5 };
  { "name": "tools/call", "id": "r1",
    "arguments": { "name": "core.echo", "arguments": { "text": "ping" } }
  }
};
```
```

**Detection Pattern:**
```regex
```\s*VISOR\s*[\r\n]+(.*?)```
```

**Regex Options:**
- `RegexOptions.Singleline` - `.` matches newlines
- `RegexOptions.IgnoreCase` - Case-insensitive `VISOR` label

**Source:** `VBlockPreParser.cs:77`

**Use Case:** Web-based LLM interfaces (ChatGPT, Claude.ai, etc.)

---

## 1.3 Terminal Framing (ECMA-48 SOS)

**Syntax:** Hidden control sequences `ESC X ... ESC \`

**Format:**
- **Start:** `ESC X` (0x1B 0x58) - Start of String
- **Content:** Same VISOR grammar as Web framing
- **End:** `ESC \` (0x1B 0x5C) - String Terminator

**Example (visualized):**
```
ESC X mcp() { { "name": "echo", "message": "ping" } }; ESC \
```

**Visibility:** Hidden from user (control sequences don't render in terminal)

**Status:** 🚧 **Planned** (not implemented in Sprint 9.1)

**Use Case:** Terminal-based LLM interfaces (Claude Code CLI, shell prompts) where visible fences would disrupt UX

**Source:** `VISOR_v1_Spec.md:81-85`

---

## 1.4 Prolog Grammar

**Format:**
```
protoName( [namedParams] ) { body } ;
```

**v1 Protocol Name:** `mcp` (only)

**Named Parameters (optional):**
| Parameter | Type | Default | Purpose |
|-----------|------|---------|---------|
| `v` | string | `"latest"` | Protocol version |
| `c` | string | `"json1s"` | Codec name |

**Examples:**
```javascript
mcp() { ... };                          // Default: v="latest", c="json1s"
mcp(v="latest") { ... };                // Explicit version
mcp(v="latest", c="json1s") { ... };   // Explicit both
mcp(c="json1s") { ... };               // Just codec
```

**Parser Implementation:**
- `Json1sParser.cs:33` checks for `mcp(` prefix
- Case-insensitive match
- Parameters currently not parsed (future enhancement)

---

## 1.5 Dotted Sections

**Syntax:**
```
.sectionName = { jsonObject };
```

**Supported Sections (v1):**

| Section | Maps To | Purpose | Security |
|---------|---------|---------|----------|
| `.headers` | `Qos.Priority` | QoS hints (priority, timeout) | ⚠️ **NO SECRETS** |
| `.meta` | `Trace`, `Schema`, `ContentType` | Metadata for observability | Safe for metadata |

**First unlabeled JSON object** = payload (MCP message)

**Example with All Sections:**
```javascript
mcp() {
  .headers = { "Priority": 5, "Timeout": 30000 };
  .meta = {
    "traceparent": "00-abc123-def456-01",
    "schema": "mcp://schemas/tools/call/1.0",
    "content_type": "application/json"
  };
  { "name": "tools/call", "id": "r1", "arguments": { "tool": "echo" } }
};
```

**Parser Implementation:**
- `Json1sParser.cs:260-386` - `ExtractDottedSectionsAndPayload()`
- Sequential state machine extracts sections
- Expects format: `.name = { ... };`
- Remaining content = payload

**Ordering Rules:**
1. Dotted sections must come before payload
2. Dotted sections can be in any order
3. Unknown sections tolerated (ignored)

**Security Note:** NEVER put secrets (API keys, passwords, tokens) in `.headers` or `.meta` - these are for hints only!

---

## 1.6 Payload Rules for mcp()

**Must be strict MCP JSON** (unchanged by VISOR)

**json1s Codec Features:**
```csharp
new JsonDocumentOptions {
    AllowTrailingCommas = true,           // Tolerates trailing commas
    CommentHandling = JsonCommentHandling.Skip  // Ignores // and /* */
};
```

**Source:** `Json1sParser.cs:15-19`

### Standard MCP Message Formats

#### Tool Call
```json
{
  "name": "tools/call",
  "id": "r1",
  "arguments": {
    "name": "core.echo",
    "arguments": { "text": "ping" }
  }
}
```

#### Resources List
```json
{
  "name": "resources/list",
  "id": "r3",
  "arguments": {
    "filter": { "type": "text/markdown" },
    "offset": 0,
    "limit": 10
  }
}
```

#### Prompts Get
```json
{
  "name": "prompts/get",
  "id": "r4",
  "arguments": {
    "name": "code_review",
    "arguments": { "code": "print('hi')" }
  }
}
```

#### Notifications Subscribe
```json
{
  "name": "notifications/subscribe",
  "id": "r2",
  "arguments": {
    "topics": ["messages", "resources"]
  }
}
```

**Source:** `VISOR_v1_Spec.md:63-136`

---

## 1.7 Reserved Roadmap Slots

### Future Codecs

| Codec | Status | Purpose |
|-------|--------|---------|
| `json1s` | ✅ Implemented | JSON strict-ish (trailing commas allowed) |
| `json1e` | 🚧 Planned | JSON embedded (special escaping) |
| `yaml1s` | 🚧 Planned | YAML strict |
| `yaml1e` | 🚧 Planned | YAML embedded |

### Batch Hints in Prolog

```javascript
mcp(p=5, t=100) { ... };  // p=priority, t=timing/latency hint (ms)
```

**Status:** 🚧 Planned (parser ignores these currently)

### New Protocol Names

| Protocol | Status | Purpose |
|----------|--------|---------|
| `mcp` | ✅ Implemented | MCP v1 protocol |
| `xmcp` | 🚧 Planned | Extended MCP with Hydra extensions |
| `http` | 🚧 Planned | HTTP-style requests |
| `grpc` | 🚧 Planned | gRPC protocol calls |

### Terminal Helpers

**Portals** - UI components in terminal
**Variables** - State management across calls

**Status:** 🚧 Long-term roadmap

**Source:** `VISOR_v1_Spec.md:137-143`

---

## Grammar Summary (EBNF-style)

```ebnf
VISOR_BLOCK     ::= FENCE_START VISOR_LABEL NEWLINE V_CALLS FENCE_END
FENCE_START     ::= "```"
VISOR_LABEL     ::= "VISOR" | "visor" | "Visor"  // case-insensitive
FENCE_END       ::= "```"

V_CALLS         ::= V_CALL+
V_CALL          ::= PROLOG BODY ";"?

PROLOG          ::= PROTO_NAME "(" PARAMS? ")"
PROTO_NAME      ::= "mcp"  // v1 only
PARAMS          ::= PARAM ("," PARAM)*
PARAM           ::= IDENTIFIER "=" STRING

BODY            ::= "{" (DOTTED_SECTION)* PAYLOAD "}"
DOTTED_SECTION  ::= "." IDENTIFIER "=" JSON_OBJECT ";"
PAYLOAD         ::= JSON_OBJECT  // MCP message

JSON_OBJECT     ::= "{" ... "}"  // Standard JSON
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1 | 2025-11-09 | Initial specification (Sprint 9.1) |

**Next Version (v1.1 planned):**
- Terminal framing implementation
- Batch hint parsing
- Additional codec support

---

**See Also:**
- [02 - Design Philosophy](./02_Design_Philosophy.md) - Why VISOR is designed this way
- [06 - Parsing Components](./06_Parsing_Components.md) - How the grammar is parsed
- [10 - Working Examples](./10_Working_Examples.md) - Real usage examples
