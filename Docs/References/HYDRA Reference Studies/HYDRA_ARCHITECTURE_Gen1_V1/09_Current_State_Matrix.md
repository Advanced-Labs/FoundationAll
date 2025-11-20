# 9. Current State Matrix

Complete status of all HYDRA components categorized by operational state.

## Component Status Table

| Component | Status | File Path | Lines | Notes |
|-----------|--------|-----------|-------|-------|
| **ChatGPTVisor** | 🚧 In Progress | `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/ChatGPTVisor.cs` | 896 | SendArbiter state machine fully implemented, SSE parsing working, WebSocket bidirectional flow operational. Registered in DI but production use unclear |
| **VisorGateway** | ⚠️ Partial | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorGateway.cs` | 384 | WebSocket gateway operational on `localhost:7777/visor`, event wiring to VisorHydraService present, registered in App.xaml.cs (DEBUG mode). No auth implemented |
| **VisorHydraService** | ⚠️ Partial | `/src/Hydra/Hydra.Server/Platform/Core Services/VisorHydraService/VisorHydraService.cs` | 304 | AutoStart=true (BootPriority=5), routes op="mcp" to XmcpClientHydraService, has working echo handler, missing full MCP server integration |
| **MCP Gateway** | ✅ Working | `/src/Hydra/Hydra.Server/Components/Gateways/MCP Gateway/HydraMcpGateway.cs` | 113 | Thin wrapper over HydraMcpSdkHost, registered and started in App.xaml.cs, actively used |
| **HydraMcpSdkHost** | ✅ Working | `/src/Hydra/Hydra.Server/Components/Gateways/MCP Gateway/HydraMcpSdkHost.cs` | 1,114 | Full MCP-SSE server implementation, OAuth integration functional, serves on `localhost:5080/mcp` |
| **PulseScheduler (Main)** | ⚠️ Partial | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/PulseScheduler.cs` | 148 | Core pulse engine with latency/byte-based batching, 3 test files, integrated with VisorGateway but some features dormant |
| **PulseScheduler (Shims)** | ⚠️ Partial | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/PulseScheduler.Shims.cs` | 154 | Legacy API compatibility layer, actively used by VisorGateway |
| **VisorConnectionQueue** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorConnectionQueue.cs` | 158 | Ring buffer with credit-based flow control, initial credit=10, max size=1000, actively used |
| **ServiceRouter** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRouter.cs` | 150 | Routes requests to services, manages HydraExecutionContext, has tests, actively used by all gateways |
| **ServiceRegistry** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRegistry.cs` | 270 | Service discovery via [HydraService], per-session instance management, has tests |
| **HydraServiceBase** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraServiceBase.cs` | 213 | Base class for all services, stats tracking, has tests, actively used |
| **HydraExecutionContext** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraExecutionContext.cs` | 64 | AsyncLocal context flow across async boundaries, has tests, actively used by ServiceRouter |
| **MessageRouter** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Core Services/VisorHydraService/MessageRouter.cs` | 216 | Reflection-based op/subject dispatch to [MessageHandler] methods, actively used by VisorHydraService |
| **HydraEnvelope** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Envelope/HydraEnvelope.cs` | 73 | Core message envelope record, heavily used (75+ references) |
| **VBlockPreParser** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Parsing/VBlockPreParser.cs` | 291 | Extracts VISOR fenced blocks via regex, has diagnostic tests, used by VisorParsePipeline |
| **Json1sParser** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Parsing/Json1sParser.cs` | 388 | Parses MCP json1s format to envelopes, handles dotted sections (.headers, .meta), used by VisorParsePipeline |
| **VisorParsePipeline** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Visor Foundation/Parsing/VisorParsePipeline.cs` | 84 | End-to-end parse pipeline (VBlockPreParser → Json1sParser), used by ChatGPTVisor and VisorGateway |
| **VisorDeterministicWriter** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorDeterministicWriter.cs` | 89 | Deterministic JSON serialization for byte-stable output, has tests, used by VisorGateway |
| **HydraSecurityService** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Core Services/HydraSecurityService.cs` | 169+partials | AutoStart=true (BootPriority=100), OAuth/OpenIddict provider on localhost:5081/5444, scope stripping implemented |
| **HydraStoreSystemV1** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Storage Foundation/HydraStoreSystemV1.cs` | 92 | AutoStart=true (BootPriority=90), Raven-backed storage, has integration tests |
| **XmcpClientHydraService** | ✅ Working | `/src/Hydra/Hydra.Server/Xmcp/XmcpClientHydraService.cs` | 251 | AutoStart=true (BootPriority=3), MCP client for external servers (e.g., echo), actively used |
| **GatewayServerBase** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Foundations/Gateway Foundation/GatewayServerBase.cs` | 290 | Base class for all gateways, provides stats tracking, has tests |
| **HydraServiceActivator** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` | 130 | Field injection via [InjectCaller*] attributes, reflection-based with caching, has tests |
| **HydraCompositionRegistry** | ✅ Working | `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraCompositionRegistry.cs` | 74 | Lamar DI registry, RavenDB + service registration, auto-discovery scanner |
| **EchoMcpServer** | 💤 Dormant | `/src/Hydra/Hydra.Server/Components/Visor/Echo/EchoMcpServer.cs` | 174 | Test/demo MCP server for development, controlled by HYDRA_TEST_ECHO env var |
| **BrowserVisorAdapter** | 💤 Dormant | `/src/Hydra/Hydra.Server/Components/UI Dependent/Browser/Visor/BrowserVisorAdapter.cs` | 113 | Bridge between browser and VISOR, coded but integration unclear |

## Summary Statistics

- **✅ Working (fully operational, tested, actively used):** 18 components
- **⚠️ Partial (operational but incomplete integration):** 4 components
- **🚧 In Progress (actively being developed):** 1 component
- **💤 Dormant (coded but not integrated/used):** 2 components

**Total Components:** 25
**Percentage Working:** 72%
**Percentage Partial/In-Progress:** 20%
**Percentage Dormant:** 8%

## What's Working ✅

### Core Infrastructure (Foundation)
- ServiceRouter - Routes requests to services with HydraExecutionContext management
- ServiceRegistry - Service discovery via [HydraService] attribute
- HydraServiceBase - Base class with stats and lifecycle management
- HydraExecutionContext - AsyncLocal context flow across async boundaries
- HydraServiceActivator - DI field injection with [InjectCaller*] attributes
- HydraCompositionRegistry - Lamar DI configuration and service registration

### VISOR Parsing Pipeline
- HydraEnvelope - Canonical message format (v0.2)
- VBlockPreParser - VISOR fence detection and extraction
- Json1sParser - json1s format parsing to envelopes
- VisorParsePipeline - End-to-end pipeline orchestration
- VisorDeterministicWriter - Deterministic JSON serialization

### Gateways
- MCP Gateway (HydraMcpGateway + HydraMcpSdkHost) - Full MCP-SSE implementation
- GatewayServerBase - Common gateway infrastructure

### Services
- XmcpClientHydraService - MCP client for external servers
- HydraSecurityService - OAuth/OpenIddict provider
- HydraStoreSystemV1 - RavenDB storage system

### Routing & Dispatch
- MessageRouter - Reflection-based [MessageHandler] dispatch
- VisorConnectionQueue - Credit-based flow control

## What's Partial ⚠️

### VisorGateway
**Status:** WebSocket gateway operational but missing auth
- ✅ WebSocket endpoint on `localhost:7777/visor`
- ✅ VISOR parsing pipeline integration
- ✅ Event wiring to VisorHydraService
- ❌ No authentication (comment: "No authentication in dev mode")
- ❌ Limited production testing

### VisorHydraService
**Status:** Auto-starts but MCP server integration incomplete
- ✅ Auto-starts with BootPriority=5
- ✅ MessageRouter op/subject dispatch
- ✅ Echo handler working
- ⚠️ Routes to XmcpClientHydraService (external MCP server)
- ❌ Missing direct MCP server integration

### PulseScheduler
**Status:** Integrated but some features underutilized
- ✅ Main engine coded with sophisticated batching
- ✅ Shim API compatibility layer
- ✅ Used by VisorGateway for pulse scheduling
- ⚠️ Some advanced features (fair-share, credit accrual) not fully exercised
- ✅ Has comprehensive test suite (3 test files)

## What's In Progress 🚧

### ChatGPTVisor
**Status:** Fully implemented but production use unclear
- ✅ SendArbiter state machine (Idle/Generating/Linger) complete
- ✅ SSE stream parsing functional
- ✅ DevTools Protocol interception working
- ✅ Bidirectional WebSocket to VisorGateway
- ✅ Micro-batching (75ms) and linger (200ms) timers
- ⚠️ Registered in DI but active use not verified
- ❌ Needs end-to-end integration testing

## What's Dormant 💤

### EchoMcpServer
**Status:** Coded for testing but not actively used
- Implements MCP echo tool for development
- Controlled by `HYDRA_TEST_ECHO` environment variable
- Has comprehensive tests (9 test methods)
- Not started by default

### BrowserVisorAdapter
**Status:** Coded but integration path unclear
- Intended as bridge between browser and VISOR
- 113 lines of code
- No references in active code paths

## Test Coverage

### Well-Tested Components
- **ServiceRouter:** ServiceRouterExecutionContextTests.cs
- **HydraExecutionContext:** ServiceRouterExecutionContextTests.cs
- **HydraServiceActivator:** ActivatorInjectionTests.cs
- **VISOR Parsers:** VisorParsingTests.cs, VisorParsing_DiagnosticTests.cs
- **VisorDeterministicWriter:** DeterministicWriterTests.cs
- **PulseScheduler:** PulseSchedulerTests.cs, PulseSchedulerBehaviorTests.cs
- **Echo Handler:** EchoToolTests.cs (9 tests)
- **End-to-End:** VisorEndToEndTests.cs

### Components Needing Tests
- ChatGPTVisor (complex state machine needs comprehensive tests)
- VisorGateway (WebSocket connection management)
- VisorHydraService (MessageRouter integration)

## Auto-Start Services (Boot Order)

Services that start automatically on application launch:

1. **XmcpClientHydraService** (Priority 3) - ✅ Working
2. **VisorHydraService** (Priority 5) - ⚠️ Partial
3. **HydraStoreSystemV1** (Priority 90) - ✅ Working
4. **HydraSecurityService** (Priority 100) - ✅ Working

## Registered Gateways

Gateways registered in App.xaml.cs:

1. **HydraMcpGateway** (Port 5080) - ✅ Working
2. **HydraApiGateway** (Port varies) - Status unclear
3. **VisorGateway** (Port 7777, DEBUG only) - ⚠️ Partial

## Critical Dependencies

### All Components Depend On:
- Lamar DI Container (✅ Working)
- HydraEnvelope format (✅ Working)
- ServiceRouter (✅ Working)

### VISOR-Specific Dependencies:
- ChatGPTVisor → VisorGateway → VisorHydraService → XmcpClientHydraService
- All ✅/⚠️/🚧 (mixed status)

### MCP-Specific Dependencies:
- MCP Client → HydraMcpGateway → HydraMcpSdkHost → Services
- All ✅ (fully working)

## Recommendations

### High Priority
1. **Complete VisorGateway auth** - Move from "dev mode" to production-ready
2. **Test ChatGPTVisor end-to-end** - Verify state machine in production scenarios
3. **Document VisorHydraService MCP routing** - Clarify when to use internal vs external MCP

### Medium Priority
4. **Integrate or remove BrowserVisorAdapter** - Resolve dormant component
5. **Expand PulseScheduler usage** - Utilize advanced batching features
6. **Add WebSocket connection tests** - Cover VisorGateway edge cases

### Low Priority
7. **Formalize EchoMcpServer** - Make it official test infrastructure
8. **Document Gen2 migration path** - Prepare for Akka.NET transition
