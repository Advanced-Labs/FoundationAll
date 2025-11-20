# VISOR Protocol & Implementation Guide (Gen1 V1) - INDEX

**Last Updated:** 2025-11-10
**Protocol Version:** v1
**Status:** MVP operational, advanced features dormant

---

## About This Documentation Set

This comprehensive guide documents the VISOR protocol specification and its current implementation in HYDRA. The documentation is split into 12 focused sections for easy navigation and reference.

**Target Audience:**
- AI developers implementing new Visor types
- Engineers modifying VISOR parsing logic
- Developers debugging state machine issues
- Anyone tracing message flow through the VISOR system

---

## Documentation Structure

### Foundation & Specification

**[01 - Protocol Specification](./01_Protocol_Specification.md)** ✅
Canonical VISOR v1 grammar, framing formats (Web/Terminal), prolog syntax, dotted sections, payload rules

**[02 - Design Philosophy](./02_Design_Philosophy.md)** ✅
Core principles, responsibility matrix, VISOR→Envelope mapping, evolvability strategy

**[03 - Visor Types](./03_Visor_Types.md)** ✅
Web Visor (fenced blocks) vs Terminal Visor (ECMA-48 SOS), implementation comparison

### Core Implementations

**[04 - ChatGPTVisor Deep Dive](./04_ChatGPTVisor.md)** ✅
Client-side harness: architecture, SendArbiter state machine, upstream/downstream flow, completion detection fix

**[05 - VisorGateway Deep Dive](./05_VisorGateway.md)** ✅
Server-side gateway: WebSocket handling, parsing pipeline, connection management, pulse scheduling

**[06 - Parsing Components](./06_Parsing_Components.md)** ✅
VBlockPreParser, Json1sParser, VisorParsePipeline - detailed implementation analysis

### Advanced Features & Operations

**[07 - Batching & Pulsing System](./07_Batching_Pulsing.md)** ✅
PulseScheduler dual nature (main engine vs shims), why main is dormant, current behavior

**[08 - Flow Control & Credit System](./08_Flow_Control.md)** ✅
VisorConnectionQueue, credit-based flow control, backpressure strategies

**[09 - Service Layer](./09_Service_Layer.md)** ✅
VisorHydraService, MessageRouter, reflection-based dispatch, handler registration

### Practical Reference

**[10 - Working Examples](./10_Working_Examples.md)** ✅
Real examples from tests: echo tool, round-trip scenarios, parsing tests

**[11 - Evolution Roadmap](./11_Evolution_Roadmap.md)** ✅
Near/mid/long-term plans, codec evolution, protocol extensions, Terminal Visor

**[12 - Troubleshooting & Performance](./12_Troubleshooting_Performance.md)** ✅
Common issues, debugging guides, performance characteristics, known limitations

---

## Quick Reference Card

| Component | File | Lines | Status |
|-----------|------|-------|--------|
| **ChatGPTVisor** | ChatGPTVisor.cs | 896 | ✅ Active |
| **VisorGateway** | VisorGateway.cs | 384 | ✅ Active |
| **VBlockPreParser** | VBlockPreParser.cs | 291 | ✅ Active |
| **Json1sParser** | Json1sParser.cs | 388 | ✅ Active |
| **PulseScheduler** | PulseScheduler.cs | 148 | 💤 Dormant |
| **PulseScheduler.Shims** | PulseScheduler.Shims.cs | 154 | ✅ Active |
| **VisorConnectionQueue** | VisorConnectionQueue.cs | 158 | ✅ Active |
| **VisorHydraService** | VisorHydraService.cs | 304 | ✅ Active |
| **MessageRouter** | MessageRouter.cs | 216 | ✅ Active |
| **VisorParsePipeline** | VisorParsePipeline.cs | 84 | ✅ Active |

**Total Implementation:** ~3,000 lines of code analyzed

---

## Key Findings

### ✅ Working (Sprint 9.1)
- Web Visor with triple-backtick fences
- ChatGPTVisor SendArbiter state machine
- SSE stream interception and parsing
- Completion detection (FIXED in Sprint 9.1)
- VisorGateway WebSocket handling
- Dual-format parsing (VISOR fences + JSON envelopes)
- Credit-based flow control
- Priority-based pulse scheduling (shims)
- Echo tool end-to-end flow

### 💤 Dormant
- PulseScheduler main engine (sophisticated batching)
- Byte-based batching
- Credit accrual and fair-share
- Terminal Visor (ECMA-48 SOS)

### 🚧 Planned
- Terminal Visor implementation
- Additional codecs (json1e, yaml1s)
- XMCP protocol support
- PulseScheduler main engine activation

---

## Navigation Tips

- **Start with 01-03** if you need protocol/design understanding
- **Jump to 04-06** for implementation details
- **Read 07-08** for advanced batching/flow control
- **Check 10** for practical examples
- **See 11-12** for roadmap and troubleshooting

---

**Research Date:** 2025-11-10
**Sprint:** 9.1 (VISOR ONE)
**Research Method:** Comprehensive code analysis + test review
