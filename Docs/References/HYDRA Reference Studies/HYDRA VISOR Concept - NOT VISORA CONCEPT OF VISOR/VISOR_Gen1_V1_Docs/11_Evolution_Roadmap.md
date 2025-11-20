# 11 - Evolution Roadmap

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

This document outlines the planned evolution of VISOR from current MVP (Sprint 9.1) through Gen1 completion and Gen2.

---

## 11.1 Near-Term (Sprint 10-12)

### Terminal Visor Implementation 🚧

**Priority:** High
**Complexity:** Medium
**Dependencies:** None

**Tasks:**
- Implement ANSI escape sequence parser (SOS...ST)
- Create TerminalVisor component (mirror ChatGPTVisor architecture)
- Integrate with Claude Code CLI
- Test in various terminal emulators
- Handle terminal state corruption gracefully

**Deliverables:**
- `TerminalVisor.cs` (est. 600-800 lines)
- ANSI parser library
- Claude Code CLI integration
- Terminal compatibility matrix

---

### PulseScheduler Main Engine Activation 🚧

**Priority:** Medium
**Complexity:** High
**Dependencies:** None

**Tasks:**
- Implement actual delivery logic in FlushAsync()
- Start ProcessAsync() background task
- Switch VisorGateway to use main API
- Add byte-size tracking to envelopes
- Test credit accrual and fair-share
- Benchmark vs shims
- Monitor queue depth in production

**Deliverables:**
- Active PulseScheduler main engine
- Performance benchmarks
- Migration guide from shims
- Monitoring dashboards

---

### Configuration Improvements 🚧

**Priority:** Low
**Complexity:** Low

**Tasks:**
- Make ChatGPTVisor timings configurable (75ms, 200ms, 150ms)
- Add VisorGateway connection limits
- Add per-connection credit configuration
- Support environment variable overrides

**Deliverables:**
- Configuration file support
- Default config with comments
- Configuration validation

---

## 11.2 Mid-Term (Gen1 Completion)

### Additional Codecs 🔮

**json1e - JSON Embedded:**
- Special escaping for embedded JSON
- Use case: JSON in JSON payloads
- Status: Spec defined, not implemented

**yaml1s - YAML Strict:**
- YAML alternative to JSON
- Cleaner syntax for humans
- Status: Planned

**yaml1e - YAML Embedded:**
- YAML with special escaping
- Status: Planned

**Implementation:**
- Add codec registry
- Implement codec interfaces
- Update Json1sParser to support codec selection
- Add tests for each codec

---

### Batch Hints in Prolog 🔮

**Syntax:**
```javascript
mcp(p=5, t=100) { ... };  // p=priority, t=timing hint (ms)
```

**Purpose:**
- Inline priority hints (alternative to .headers)
- Latency requirements
- Batch optimization hints

**Implementation:**
- Parse prolog named parameters
- Map to envelope Qos fields
- Support both prolog hints and .headers (prolog wins)

---

### XMCP Protocol Support 🔮

**XMCP = Extended MCP with Hydra features:**
- Streaming responses
- Bidirectional subscriptions
- Service discovery
- Capability negotiation

**Example:**
```javascript
xmcp() {
  .headers = { "Priority": 5, "Stream": true };
  { "name": "tools/call", "id": "r1", ... }
};
```

**Implementation:**
- New prolog: `xmcp(...)`
- Extended Json1sParser
- XMCP-specific envelope fields
- Integration with XmcpClientHydraService

---

### HTTP/gRPC Protocol Support 🔮

**HTTP:**
```javascript
http() {
  .headers = { "Method": "POST", "Path": "/api/v1/chat" };
  { "body": "..." }
};
```

**gRPC:**
```javascript
grpc() {
  .headers = { "Service": "ChatService", "Method": "SendMessage" };
  { "request": "..." }
};
```

**Status:** Research phase

---

## 11.3 Long-Term (Gen2)

### Terminal Advanced Features 🔮

**Portals:**
- UI components in terminal (progress bars, tables)
- Embedded in VISOR blocks
- Rendered by terminal harness

**Variables:**
- State management across VISOR calls
- Reference previous results
- Reduce payload duplication

**Example:**
```javascript
mcp() {
  .portal = { "type": "progress", "id": "p1" };
  { "name": "task/start", "portalId": "p1" }
};
```

---

### Hot-Reloadable Parsers 🔮

**Goal:** Add new codecs without recompiling

**Approach:**
- Plugin architecture
- Dynamic codec loading
- Isolated codec sandboxes
- Version negotiation

---

### Distributed VISOR 🔮

**Features:**
- Multi-node routing
- Load balancing across gateways
- Shared pulse scheduler
- Distributed tracing

**Use Case:** High-scale deployments

---

### Binary VISOR 🔮

**Format:**
```
[Magic: 4 bytes "VISR"]
[Version: 1 byte]
[Codec: 1 byte]
[Length: 4 bytes]
[Content: protobuf/msgpack]
```

**Benefits:**
- Smaller payloads
- Faster parsing
- Mobile-friendly

**Trade-off:** Not human-readable

---

## 11.4 Evolvability Guarantees

### Backward Compatibility

**Forever valid:**
```javascript
// This v1 fence will always work
mcp() { { "name": "echo" } };
```

**Rules:**
- Can't remove support for existing codecs
- Can't change grammar incompatibly
- Can't break envelope field meanings
- Old clients work with new servers

### Forward Compatibility

**Tolerance:**
- Unknown dotted sections ignored
- Unknown prolog params skipped
- Unknown envelope fields preserved
- Graceful degradation

---

## 11.5 Deprecation Policy

**Minimum Support:** 3 major versions

**Example:**
- v1 introduced in Sprint 9 (2025-11)
- v2 planned for Sprint 15 (2026-02)
- v3 planned for Sprint 20 (2026-05)
- v1 support until v4 (2026-08 earliest)

**Deprecation Process:**
1. Announce deprecation (1 sprint notice)
2. Mark as deprecated in docs
3. Emit warnings in logs
4. Remove after 3 versions

---

## 11.6 Migration Paths

### Shims → Main Engine

**When:** Sprint 11-12

**Steps:**
1. Feature flag to enable main engine
2. Run both in parallel (compare results)
3. Gradually roll out to connections
4. Remove shims after 100% migration

### Web Visor → Terminal Visor

**When:** Sprint 10-11

**Steps:**
1. Implement Terminal Visor
2. Detect environment (web vs terminal)
3. Use appropriate visor type
4. Both supported indefinitely

### json1s → yaml1s

**When:** Gen1 completion

**Steps:**
1. Client requests yaml1s via `c` parameter
2. Server detects and uses YAML parser
3. Both codecs supported simultaneously
4. No breaking changes

---

## 11.7 Research Topics

### Performance Optimization
- Zero-copy parsing with Span<char>
- Compiled regex caching
- Parser pooling
- Streaming parser (incremental)

### Security
- Content-Security-Policy for VISOR blocks
- Sanitization of dotted sections
- Rate limiting per client
- Payload size limits

### Observability
- Distributed tracing integration
- Metrics export (Prometheus)
- Structured logging
- Debug mode with verbose output

### Testing
- Fuzzing for parser robustness
- Property-based testing
- Load testing harness
- Chaos engineering

---

**See Also:**
- [01 - Protocol Specification](./01_Protocol_Specification.md) - Reserved slots
- [02 - Design Philosophy](./02_Design_Philosophy.md) - Evolvability strategy
- [12 - Troubleshooting](./12_Troubleshooting_Performance.md) - Current limitations
