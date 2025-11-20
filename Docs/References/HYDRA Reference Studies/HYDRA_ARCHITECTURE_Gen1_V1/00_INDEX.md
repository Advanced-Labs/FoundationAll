# HYDRA Architecture Deep Dive (Gen1 V1)

**Last Updated:** 2025-11-10
**Scope:** Current implementation (Sprint 9.1)
**Status Legend:** ✅ Working | ⚠️ Partial | 🚧 In Progress | 💤 Dormant (coded but unused)

## Purpose

This is the comprehensive technical reference for AI developers who need to understand HOW HYDRA works internally. This documentation is for agents who need to:

- Understand complete data flow from user input to service execution
- Modify gateway behavior or add new gateways
- Debug WebSocket/DevTools Protocol issues
- Understand auth and security architecture
- Know what's implemented vs what's designed-but-dormant

## Documentation Structure

1. [**System Topology**](01_System_Topology.md) - Component diagram, boundaries, what crosses where
2. [**The Three-Layer Model**](02_Three_Layer_Model.md) - VISOR → Envelope → Services breakdown
3. [**Complete Data Flow**](03_Complete_Data_Flow.md) - Upstream & downstream flows with line numbers
4. [**Component Deep Dive**](04_Component_Deep_Dive.md) - ChatGPTVisor, VisorGateway, Services, PulseScheduler, Queues
5. [**Authentication & Authorization**](05_Authentication.md) - Three-layer model, OAuth, current state
6. [**Storage System**](06_Storage_System.md) - RavenDB, HydraStoreSystemV1, OpenIddict integration
7. [**Execution Context**](07_Execution_Context.md) - AsyncLocal, field injection patterns
8. [**DI Integration**](08_DI_Integration.md) - Lamar registry, lifetimes, activation
9. [**Current State Matrix**](09_Current_State_Matrix.md) - What's working, partial, planned, dormant
10. [**Integration Points**](10_Integration_Points.md) - Gateway types, service discovery
11. [**Critical Constraints**](11_Critical_Constraints.md) - Boundaries, evolvability, OAuth invariants
12. [**Testing Strategy**](12_Testing_Strategy.md) - Organization, key test files
13. [**Gen2 Vision**](13_Gen2_Vision.md) - Akka.NET, Wolverine, EventStoreDB, migration path

## Quick Reference

### Key Components by Status

- **✅ Working:** ServiceRouter, HydraExecutionContext, HydraEnvelope, VISOR Parsers, MCP Gateway, Security Service
- **⚠️ Partial:** VisorGateway, VisorHydraService, PulseScheduler
- **🚧 In Progress:** ChatGPTVisor
- **💤 Dormant:** EchoMcpServer, BrowserVisorAdapter

### Critical File Paths

| Component | Path | Lines |
|-----------|------|-------|
| ChatGPTVisor | `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs` | 896 |
| VisorGateway | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorGateway.cs` | 384 |
| VisorHydraService | `/src/Hydra/Hydra.Server/Platform/Core Services/VisorHydraService/VisorHydraService.cs` | 304 |
| HydraEnvelope | `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Envelope/HydraEnvelope.cs` | 73 |
| ServiceRouter | `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRouter.cs` | 150 |

## Gen1 vs Gen2

**Gen1 V1** (Current): Direct CLR calls, WebSocket gateways, session-scoped services, simple routing

**Gen2** (Future): Akka.NET for location transparency, Wolverine for durable sagas, EventStoreDB for event sourcing, hot-loadable components

The envelope format remains canonical across both generations.
