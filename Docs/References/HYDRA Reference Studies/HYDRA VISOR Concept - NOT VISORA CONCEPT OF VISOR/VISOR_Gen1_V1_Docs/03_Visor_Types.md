# 03 - Visor Types & Implementations

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

This document describes the two VISOR implementation types (Web and Terminal), their differences, and implementation status.

---

## 3.1 Web Visor (Fenced Blocks)

### Syntax

Triple-backtick markdown fences labeled `VISOR`:

```
```VISOR
mcp() { { "name": "echo", "message": "ping" } };
```
```

### Detection Method

**Regex pattern matching** on accumulated text buffer

```csharp
private readonly Regex _visorFenceRegex = new Regex(
    @"```visor\s*\n(.*?)\n```",
    RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase
);
```

**Source:** `ChatGPTVisor.cs:77`

### Characteristics

| Characteristic | Description |
|---------------|-------------|
| **Visibility** | Visible in chat (fences appear in conversation) |
| **User Experience** | Acceptable for web interfaces where markdown is common |
| **Detection** | Simple regex match |
| **Parsing** | Standard text processing |
| **Error Handling** | Malformed fences skipped gracefully |

### Current Implementations

✅ **ChatGPTVisor** - Client-side harness for ChatGPT
- **File:** `ChatGPTVisor.cs`
- **Lines:** 896
- **Status:** Operational (Sprint 9.1)
- **Features:**
  - SSE stream interception
  - SendArbiter state machine
  - Upstream micro-batching (75ms)
  - Downstream buffering with linger (200ms)
  - Completion detection via `message_stream_complete`

### Use Cases

- **ChatGPT web interface** - Works today
- **Claude.ai** - Would work (not tested)
- **Other web LLM chats** - Compatible with any markdown-supporting interface
- **Browser extensions** - Can inject VISOR into any chat UI

### Limitations

- Visible fences clutter conversation history
- User can see VISOR blocks in UI
- Copy/paste includes VISOR syntax
- Not suitable for clean UX requirements

---

## 3.2 Terminal Visor (ECMA-48 SOS)

### Syntax

**ANSI escape sequences:** `ESC X ... ESC \`

```
ESC X mcp() { { "name": "echo", "message": "ping" } }; ESC \
```

Where:
- `ESC X` = `0x1B 0x58` (Start of String)
- `ESC \` = `0x1B 0x5C` (String Terminator)

### Detection Method

**ANSI escape sequence parsing** in terminal output stream

**Pseudocode:**
```
while reading terminal output:
    if byte == 0x1B and next_byte == 0x58:
        start capturing
        buffer = ""
        while true:
            byte = read()
            if byte == 0x1B and next_byte == 0x5C:
                break  # End of VISOR block
            buffer += byte
        parse(buffer)  # Contains VISOR grammar (same as Web)
```

### Characteristics

| Characteristic | Description |
|---------------|-------------|
| **Visibility** | **Hidden from user** (control sequences don't render) |
| **User Experience** | Clean - user never sees VISOR syntax |
| **Detection** | Requires ANSI parser |
| **Parsing** | Same grammar as Web after extraction |
| **Error Handling** | Malformed sequences may corrupt terminal state |

### Current Implementations

❌ **None** - Not implemented in Sprint 9.1

**Planned Implementation:**
- **TerminalVisor** (future component)
- Integration with Claude Code CLI
- ANSI parser for SOS...ST detection
- Same parsing pipeline as Web (reuse VBlockPreParser + Json1sParser)

### Use Cases

- **Claude Code CLI** - Primary target
- **Shell prompts** - LLM-enhanced shell
- **Terminal-based LLM tools** - Any terminal interface
- **SSH sessions** - Clean remote LLM interaction

### Advantages over Web Visor

1. **Invisible** - User never sees protocol framing
2. **Clean UX** - No clutter in conversation
3. **Copy/paste friendly** - No syntax pollution
4. **Professional** - Suitable for production tools

### Technical Challenges

1. **ANSI parsing complexity** - Need robust parser
2. **Terminal state management** - Invalid sequences can break terminal
3. **Compatibility** - Not all terminals support all escape sequences
4. **Testing** - Harder to debug (invisible output)

**Status:** 🚧 Planned for future sprint

**Source:** `VISOR_v1_Spec.md:81-85`

---

## 3.3 Implementation Comparison

| Feature | Web Visor | Terminal Visor |
|---------|-----------|----------------|
| **Framing** | ` ```VISOR ... ``` ` | `ESC X ... ESC \` |
| **Visibility** | ✅ Visible in chat | ❌ Hidden from user |
| **Detection** | Regex on text | ANSI escape parser |
| **Grammar Inside** | VISOR v1 spec | **Identical** VISOR v1 spec |
| **Parsing After Detection** | VBlockPreParser → Json1sParser | **Same** pipeline |
| **Status** | ✅ Implemented (ChatGPTVisor) | 🚧 Planned (TerminalVisor) |
| **Lines of Code** | 896 lines (ChatGPTVisor.cs) | Not yet implemented |
| **Use Case** | Web LLM interfaces | Terminal LLM tools |
| **UX Impact** | Clutters conversation | Clean, professional |
| **Testing** | Easy (visible output) | Harder (invisible) |
| **Compatibility** | Works in any web interface | Terminal-dependent |
| **Error Handling** | Graceful (skip bad fences) | Risky (can corrupt terminal) |

**Key Insight:** Only the **outer framing** differs. The inner **VISOR grammar is identical** for both types.

---

## 3.4 Shared Components

Both Visor types share the same parsing pipeline after frame extraction:

```
[Web Visor]
  ChatGPTVisor → Regex → Strip fences → VBlocks
                                           ↓
                                    [Shared Pipeline]
                                    VBlockPreParser
                                           ↓
                                    Json1sParser
                                           ↓
                                    HydraEnvelopes
                                           ↑
  TerminalVisor → ANSI parser → Extract SOS...ST → VBlocks
[Terminal Visor]
```

**Shared Files:**
- `VBlockPreParser.cs` (291 lines) - Extracts V-calls from blocks
- `Json1sParser.cs` (388 lines) - Parses V-calls to envelopes
- `VisorParsePipeline.cs` (84 lines) - Orchestrates the pipeline

---

## 3.5 Future Visor Types

### 3.5.1 HTTP Header Visor

**Concept:** VISOR blocks in HTTP headers

```http
POST /chat HTTP/1.1
X-VISOR-1: mcp() { { "name": "echo",
X-VISOR-2:   "message": "ping" } };
Content-Type: text/plain

User's regular message here
```

**Status:** 🚧 Possible future extension
**Use Case:** HTTP APIs, REST clients

---

### 3.5.2 WebSocket Metadata Visor

**Concept:** VISOR blocks in WebSocket frames

```javascript
websocket.send(JSON.stringify({
  type: "message",
  text: "User message",
  visor: "mcp() { { \"name\": \"echo\" } };"
}));
```

**Status:** 🚧 Possible future extension
**Use Case:** Direct WebSocket connections

---

### 3.5.3 Binary Visor

**Concept:** Binary-encoded VISOR for efficiency

```
[Length: 4 bytes][Magic: "VISR"][Version: 1][Content: binary protobuf/msgpack]
```

**Status:** 🚧 Long-term consideration
**Use Case:** High-throughput scenarios, mobile apps

---

## 3.6 Choosing a Visor Type

| Requirement | Recommended Type |
|-------------|------------------|
| Web LLM interface (ChatGPT, Claude.ai) | **Web Visor** |
| Terminal/CLI tool (Claude Code) | **Terminal Visor** |
| HTTP API | **HTTP Header Visor** (future) |
| WebSocket connection | **WebSocket Metadata** (future) |
| High throughput | **Binary Visor** (future) |
| Need visibility for debugging | **Web Visor** |
| Need clean UX | **Terminal Visor** |

---

## 3.7 Implementation Roadmap

### Sprint 9.1 (Current) ✅
- Web Visor (ChatGPTVisor) fully operational
- Shared parsing pipeline implemented

### Sprint 10-11 (Planned) 🚧
- Terminal Visor (TerminalVisor) implementation
- ANSI escape sequence parser
- Claude Code CLI integration

### Sprint 12+ (Future) 🔮
- HTTP Header Visor exploration
- WebSocket Metadata Visor
- Binary Visor research

---

**See Also:**
- [01 - Protocol Specification](./01_Protocol_Specification.md) - VISOR grammar (shared by all types)
- [04 - ChatGPTVisor](./04_ChatGPTVisor.md) - Web Visor implementation details
- [06 - Parsing Components](./06_Parsing_Components.md) - Shared parsing pipeline
