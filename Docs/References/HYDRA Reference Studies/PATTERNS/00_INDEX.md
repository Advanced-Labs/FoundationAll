# HYDRA Design Patterns - Index

**Last Updated:** 2025-11-10
**Purpose:** Reusable .NET design patterns extracted from HYDRA for use in other projects
**Target Audience:** AI agents implementing similar patterns on .NET platform

---

## About This Pattern Library

HYDRA was an R&D project exploring platform design patterns for multi-protocol, multi-backend orchestration systems. While HYDRA itself is incomplete and Gen1 is temporary (Gen2 planned with Akka.NET), the **patterns it implements are valuable and reusable**.

This documentation extracts these patterns in a way that AI agents can:
1. **Understand** the pattern intent, problem solved, and tradeoffs
2. **Reproduce** the pattern in other .NET projects with step-by-step guides
3. **Adapt** the pattern to different contexts with clear design decision rationale

Each pattern document includes:
- Pattern intent and motivation
- Complete HYDRA implementation with file references and line numbers
- Key design decisions with rationales
- Step-by-step reproduction guide for other projects
- Tradeoffs and when NOT to use the pattern
- Gen2 evolution notes (how pattern will change in next generation)

---

## Pattern Catalog

### Infrastructure Patterns

#### [Gateway Pattern](./Gateway_Pattern.md) - Protocol-Agnostic Server Adapters
**Complexity:** Medium | **Reusability:** High

Uniform abstraction for different protocol servers (WebSocket, HTTP, SSE) with consistent lifecycle management, connection tracking, and statistics collection.

**Key Concepts:**
- Template method pattern for lifecycle (OnStartAsync/OnStopAsync)
- Per-connection activity tracking (15-second timeout)
- Thread-safe stats collection
- Centralized registry and discovery

**When to Use:** Building platforms that accept connections over multiple protocols

**Files:** `GatewayServerBase.cs` (290 lines), `GatewayRegistry.cs` (141 lines), `VisorGateway.cs` (384 lines)

---

### Integration Patterns

#### [Envelope Messaging Pattern](./Envelope_Messaging_Pattern.md) - Protocol-Agnostic Message Wrapper
**Complexity:** Medium | **Reusability:** Very High

Universal message envelope with forward-compatible schema evolution, deterministic serialization, and complete transport decoupling.

**Key Concepts:**
- Evolvable envelope (add-only contract, unknown fields ignored)
- Canonical JSON with lexicographically sorted keys
- Services never see envelopes (unwrapped by router)
- C# records for immutability

**When to Use:** Multi-protocol messaging systems needing version-safe message format

**Files:** `HydraEnvelope.cs` (73 lines), `VisorCanonicalizer.cs` (106 lines), `VisorDeterministicWriter.cs` (89 lines)

---

### Framework Patterns

#### [Service Discovery & DI Pattern](./Service_Discovery_DI_Pattern.md) - Attribute-Based Registration
**Complexity:** Medium-High | **Reusability:** High

Automatic service discovery via attributes, per-session instance caching, scoped DI activation, and field injection via AsyncLocal.

**Key Concepts:**
- Attribute-based discovery (`[HydraService]`)
- Per-session singleton instances (static registry)
- DI factory + instance caching
- Field injection for caller context

**When to Use:** Plugin-based systems needing auto-discovery and stateful services

**Files:** `HydraServiceBase.cs` (214 lines), `ServiceRegistry.cs` (271 lines), `ServiceRouter.cs` (151 lines)

---

### Data Processing Patterns

#### [Parsing Pipeline Pattern](./Parsing_Pipeline_Pattern.md) - Multi-Stage State Machine Parsing
**Complexity:** High | **Reusability:** High

Robust multi-stage parsing with sequential character-by-character state machines, balanced delimiter tracking, and graceful error recovery.

**Key Concepts:**
- Sequential character parser (position-based)
- Depth tracking with string awareness
- Partial result return on errors
- Section isolation (malformed section doesn't break parse)

**When to Use:** Parsing complex nested formats with robust error recovery

**Files:** `VBlockPreParser.cs` (291 lines), `Json1sParser.cs` (388 lines), `VisorParsePipeline.cs` (84 lines)

---

### Concurrency Patterns

#### [Flow Control & Batching Pattern](./Flow_Control_Batching_Pattern.md) - Credit-Based Backpressure
**Complexity:** Medium-High | **Reusability:** High

Credit-based flow control to prevent message flooding, per-connection queuing for isolation, and priority-based scheduling.

**Key Concepts:**
- Credit-based backpressure (receiver controls send rate)
- Per-connection bounded queues
- Priority scheduling with latency constraints
- Client-side micro-batching (75ms window)

**When to Use:** Bidirectional systems where server needs to control send rate per client

**Files:** `VisorConnectionQueue.cs` (158 lines), `VisorGateway.cs` (384 lines), `PulseScheduler.Shims.cs` (154 lines)

---

### Cross-Cutting Patterns

#### [Execution Context Pattern](./Execution_Context_Pattern.md) - AsyncLocal Context Flow
**Complexity:** Medium | **Reusability:** Very High

Flow caller context (connection, session, auth) through async call chains without polluting method signatures, using AsyncLocal for async/await safety.

**Key Concepts:**
- AsyncLocal<T> for async-safe context flow
- Stack-based push/pop with IDisposable
- Field injection from ambient context
- Immutable context (set once per request)

**When to Use:** Async systems needing implicit context flow without parameter passing

**Files:** `HydraExecutionContext.cs` (65 lines), `HydraServiceActivator.cs` (129 lines), `CallerInjectionAttributes.cs` (36 lines)

---

### Quality Assurance Patterns

#### [Testing Patterns](./Testing_Patterns.md) - Multi-Layer Test Strategy
**Complexity:** Medium | **Reusability:** High

Comprehensive test coverage with unit tests, integration tests, E2E tests, UI automation (FAT framework), and test vectors for parsing validation.

**Key Concepts:**
- Test pyramid (unit → integration → E2E)
- Unique session IDs to avoid test pollution
- Arrange-Act-Assert pattern
- FAT framework for WPF UI automation
- Test vectors for parser validation

**When to Use:** Complex layered systems needing multiple test layers

**Files:** `Hydra.Tests/` directory structure, various test files

---

## Pattern Relationships

### Pattern Synergies

**Gateway + Envelope:**
- Gateways convert protocol-specific messages to canonical envelopes
- Example: VisorGateway parses VISOR blocks → HydraEnvelope

**Envelope + Service Discovery:**
- Services receive unwrapped envelope payloads
- Router extracts payload from envelope before service invocation

**Service Discovery + Execution Context:**
- Router pushes execution context before service invocation
- Services access context via AsyncLocal or field injection

**Parsing + Envelope:**
- Parsers output HydraEnvelope instances
- Parsing errors don't prevent envelope creation (partial data)

**Flow Control + Gateway:**
- Gateways implement per-connection flow control
- Gateway send loops respect credit limits

**Execution Context + Testing:**
- Tests use unique session IDs to isolate execution contexts
- Prevents static registry pollution across tests

---

## Pattern Selection Guide

### By Use Case

| Use Case | Recommended Patterns |
|----------|---------------------|
| Multi-protocol server | Gateway + Envelope |
| Plugin architecture | Service Discovery + DI |
| Complex format parsing | Parsing Pipeline |
| Async context flow | Execution Context |
| High-throughput bidirectional | Flow Control + Batching |
| Comprehensive testing | Testing Patterns |

### By Complexity

| Complexity | Patterns |
|-----------|----------|
| **Low** | Execution Context |
| **Medium** | Gateway, Envelope, Flow Control, Testing |
| **High** | Parsing Pipeline, Service Discovery + DI |

### By Reusability

| Reusability | Patterns |
|-------------|----------|
| **Very High** | Envelope, Execution Context |
| **High** | Gateway, Service Discovery, Parsing, Flow Control, Testing |

---

## Anti-Patterns to Avoid

Based on HYDRA's Gen1 limitations:

### ❌ Static Registry Without Cleanup
**Problem:** Per-session instances cached in static dictionary forever
**Solution:** Implement cleanup on session end or use scoped lifetimes

### ❌ Reflection Without Caching
**Problem:** Repeated reflection calls on hot path (performance)
**Solution:** Cache reflection metadata (see `InjectionDescriptor` cache)

### ❌ ThreadLocal for Async
**Problem:** ThreadLocal breaks with async/await (thread switches)
**Solution:** Use AsyncLocal<T> instead

### ❌ Shared Test State
**Problem:** Reused session IDs across tests cause pollution
**Solution:** Generate unique session ID per test (`Guid.NewGuid()`)

### ❌ Unbounded Queues
**Problem:** Memory leak on slow consumers
**Solution:** Bounded queues with backpressure (reject when full)

### ❌ Simple String Split for Nested Formats
**Problem:** Splits inside strings, nested objects
**Solution:** Depth-aware splitting with string context tracking

---

## Gen2 Evolution Summary

All patterns documented here are **Gen1 implementations**. HYDRA Gen2 will replace many internals while keeping pattern concepts:

| Pattern | Gen1 | Gen2 (Planned) |
|---------|------|----------------|
| Gateway | In-process, direct CLR calls | Akka.NET actors, location transparent |
| Envelope | In-memory CLR objects | Event-sourced (EventStoreDB) |
| Service Discovery | Reflection-based | Source-generated |
| Parsing | Hand-written state machines | Parser generators (ANTLR) |
| Flow Control | In-memory queues | Distributed queues, Akka mailboxes |
| Execution Context | AsyncLocal<T> | Akka context + OpenTelemetry baggage |
| Testing | xUnit, FAT framework | + Property-based testing (FsCheck) |

**Migration Strategy:** Pattern concepts remain stable, implementations shift to actor model + event sourcing.

---

## How to Use This Library

### For Understanding HYDRA
1. Start with **Gateway Pattern** to understand protocol abstraction
2. Read **Envelope Messaging Pattern** to understand message format
3. Review **Service Discovery & DI Pattern** to understand service layer

### For Implementing Similar Patterns
1. Identify which patterns apply to your use case
2. Read pattern document completely (intent → implementation → reproduction)
3. Follow "Reproducing This Pattern" section step-by-step
4. Adapt to your specific requirements

### For Learning .NET Patterns
1. Study implementation details (file references, line numbers)
2. Review key design decisions and rationales
3. Understand tradeoffs and when NOT to use
4. Compare with related patterns

---

## Contributing Notes for AI Agents

When extending HYDRA or documenting new patterns:

1. **File References:** Always include file paths and line numbers
2. **Code Examples:** Show actual implementation, not pseudocode
3. **Reproduction Guide:** Step-by-step guide for other projects
4. **Tradeoffs:** Explicitly state advantages, limitations, anti-patterns
5. **Gen2 Notes:** How pattern will evolve in next generation

---

## Related Documentation

### HYDRA System Documentation
- **[HYDRA Quickstart](../HYDRA_QUICKSTART_Gen1_V1.md):** System overview, navigation, current state
- **[HYDRA Architecture](../HYDRA_ARCHITECTURE_Gen1_V1/):** 13 deep-dive docs on topology, data flow, components
- **[Service Foundation Guide](../UP-TO-DATE/HYDRA_SERVICE_FOUNDATION_Gen1_V1.md):** Complete service pattern reference
- **[VISOR Protocol Guide](../UP-TO-DATE%20-%20Still%20check%20with%20Code%20and%20User/VISOR/VISOR_Gen1_V1_Docs/):** 12 docs on VISOR protocol specification and implementation

### External Resources
- **.NET AsyncLocal:** [Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1)
- **Akka.NET:** [akka.net](https://getakka.net/) (Gen2 foundation)
- **EventStoreDB:** [eventstore.com](https://www.eventstore.com/) (Gen2 persistence)
- **xUnit:** [xunit.net](https://xunit.net/) (testing framework)

---

## Pattern Stability Matrix

| Pattern | Gen1 Stability | Gen2 Compatibility | Complexity | Learning Curve |
|---------|---------------|-------------------|-----------|---------------|
| Gateway Pattern | High | High (concepts remain) | Medium | Low-Medium |
| Envelope Messaging | Very High | Very High (format stable) | Medium | Medium |
| Service Discovery | Medium | Low (will be generated) | High | Medium-High |
| Parsing Pipeline | Medium | Low (may use generators) | High | High |
| Flow Control | High | High (protocol stable) | Medium-High | Medium |
| Execution Context | Very High | High (+ OpenTelemetry) | Medium | Low |
| Testing Patterns | High | High (structure stable) | Medium | Low-Medium |

**Stability Legend:**
- **Very High:** Unlikely to change significantly
- **High:** Minor changes only
- **Medium:** Moderate changes expected
- **Low:** Major refactoring planned

---

## Quick Reference Card

### Pattern Selection Flowchart

```
Need to support multiple protocols?
  ├─ Yes → Use Gateway Pattern
  └─ No → Skip

Need version-safe message format?
  ├─ Yes → Use Envelope Messaging Pattern
  └─ No → Skip

Need auto-discovery of services?
  ├─ Yes → Use Service Discovery & DI Pattern
  └─ No → Skip

Need to parse complex nested formats?
  ├─ Yes → Use Parsing Pipeline Pattern
  └─ No → Skip

Need flow control for bidirectional messaging?
  ├─ Yes → Use Flow Control & Batching Pattern
  └─ No → Skip

Need implicit context flow in async code?
  ├─ Yes → Use Execution Context Pattern
  └─ No → Skip

Need comprehensive test coverage?
  ├─ Yes → Use Testing Patterns
  └─ No → Minimal testing
```

---

**Pattern Library Version:** 1.0 (Gen1)
**Total Patterns:** 7
**Total Documentation:** ~25KB across 7 files
**Code References:** ~3,500 lines of implementation analyzed
**Last Updated:** 2025-11-10

---

**End of Index**

For questions or clarifications, refer to source code in `/src/Hydra/` directory.
