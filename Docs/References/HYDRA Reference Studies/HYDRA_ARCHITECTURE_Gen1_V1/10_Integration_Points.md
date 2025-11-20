# 10. Integration Points

## 10.1 Gateway Types

### VisorGateway (WebSocket, Partial)

**File:** `/src/Hydra/Hydra.Server/Platform/Core Gateways/VisorGateway/VisorGateway.cs` (384 lines)
**Status:** ⚠️ Partial (no auth)
**Port:** 7777
**Protocol:** WebSocket
**Path:** `/visor`
**Registered:** Yes (DEBUG mode only in App.xaml.cs)

**Features:**
- Dual-format ingress (JSON envelope or VISOR fences)
- VISOR parsing pipeline integration
- Credit-based flow control
- Pulse scheduling (shim API)

**Missing:**
- Authentication/authorization
- Production deployment config
- Connection limits
- Rate limiting

**Event:**
```csharp
public event EventHandler<EnvelopesReceivedEventArgs>? EnvelopesReceived;
```

**Wiring (App.xaml.cs Line 153):**
```csharp
visorGateway.EnvelopesReceived += async (sender, args) => {
    // Get VisorHydraService
    // Call ProcessEnvelopes
    // Send response
};
```

### MCP Gateway (HTTP/SSE, Working)

**File:** `/src/Hydra/Hydra.Server/Components/Gateways/MCP Gateway/HydraMcpGateway.cs` (113 lines)
**Status:** ✅ Working
**Port:** 5080
**Protocol:** HTTP + Server-Sent Events (SSE)
**Paths:**
- `/mcp` - MCP endpoint
- `/oauth` - OAuth integration
**Registered:** Yes (App.xaml.cs)

**Features:**
- Full MCP protocol implementation
- OAuth/OpenIddict integration
- SSE streaming for tool results
- Bearer token validation

**Delegation:**
- Thin wrapper over `HydraMcpSdkHost`
- HydraMcpSdkHost (1,114 lines) does heavy lifting

### XMCP Gateway (Planned)

**Status:** 🚧 Planned
**Purpose:** Extended MCP protocol with HYDRA enhancements
**Features (Planned):**
- Backwards-compatible with MCP
- Native envelope support
- Macaroon capabilities
- Event subscriptions

## 10.2 Service Discovery

### Reflection-Based via [HydraService]

**Attribute:**
```csharp
[HydraService(
    Name = "VisorHydraService",
    Description = "Visor protocol service with op/subject routing",
    AutoStart = true,
    BootPriority = 5
)]
public partial class VisorHydraService : HydraServiceBase
{
    // ...
}
```

**Discovery (ServiceRegistry.cs Lines 38-54):**
```csharp
var serviceTypes = assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(IHydraService).IsAssignableFrom(t))
    .Where(t => t.GetCustomAttribute<HydraServiceAttribute>() != null);

foreach (var type in serviceTypes)
{
    var attr = type.GetCustomAttribute<HydraServiceAttribute>()!;
    Register(attr.Name, type);
}
```

**ServiceRegistry.Global.DiscoverServices()**
- Scans executing assembly
- Filters by `IHydraService` interface + `[HydraService]` attribute
- Registers in global registry

### Auto-Start via BootPriority

**ServiceBootManager (Lines 23-63):**
```csharp
public static async Task AutoStartHostedServicesAsync(
    ServiceRegistry registry,
    IServiceProvider serviceProvider,
    CancellationToken ct)
{
    var items = registry.GetAll()
        .Select(n => (Name: n, Attr: registry.GetServiceAttribute(n)!, Type: registry.GetServiceType(n)!))
        .Where(t => t.Attr.AutoStart)
        .OrderBy(t => t.Attr.BootPriority)  // Lower starts first
        .ToList();

    foreach (var (name, attr, type) in items)
    {
        var inst = registry.GetOrCreateInstance(name, SystemSession, serviceProvider);
        
        if (inst is IHydraHostedService host)
        {
            await host.StartHostAsync(ct);
        }
    }
}
```

**Boot Order:**
1. XmcpClientHydraService (3)
2. VisorHydraService (5)
3. HydraStoreSystemV1 (90)
4. HydraSecurityService (100)

## 10.3 Gateway-Service Wiring

### Event-Based Pattern

**Gateway Interface:**
```csharp
public interface IHydraGateway
{
    event EventHandler<EnvelopesReceivedEventArgs>? EnvelopesReceived;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

**EventArgs:**
```csharp
public class EnvelopesReceivedEventArgs : EventArgs
{
    public string ConnectionId { get; }
    public IReadOnlyList<HydraEnvelope> Envelopes { get; }
}
```

**Wiring (App.xaml.cs Pattern):**
```csharp
gateway.EnvelopesReceived += async (sender, args) =>
{
    // 1. Get appropriate service from DI
    var service = _container.GetInstance<VisorHydraService>();
    
    // 2. Prepare parameters
    var parameters = new Dictionary<string, object?> {
        ["envelopes"] = args.Envelopes
    };
    
    // 3. Create invocation context
    var context = new ServiceInvocationContext {
        SessionId = args.ConnectionId,
        GatewayName = gateway.Name
    };
    
    // 4. Invoke service
    var result = await service.InvokeAsync("ProcessEnvelopes", parameters, context, cancellationToken);
    
    // 5. Send response
    if (result is IEnumerable<HydraEnvelope> responseEnvelopes) {
        foreach (var envelope in responseEnvelopes) {
            gateway.EnqueueEnvelope(args.ConnectionId, envelope);
        }
    }
};
```

### GatewayRegistry Pattern

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Gateway Foundation/GatewayRegistry.cs`

**Purpose:** Manage multiple gateways
**Features:**
- Register/unregister gateways
- Start all / stop all
- Lifecycle management
- Stats aggregation

**Usage (App.xaml.cs Lines 123-257):**
```csharp
var gatewayRegistry = new GatewayRegistry();

// Register gateways
gatewayRegistry.Register(mcpGateway);
gatewayRegistry.Register(apiGateway);
gatewayRegistry.Register(visorGateway);

// Start all
await gatewayRegistry.StartAllAsync(cancellationToken);
```

## 10.4 Service-to-Service Communication

### ServiceRouter Pattern

**Direct Invocation:**
```csharp
var result = await ServiceRouter.InvokeAsync(
    serviceName: "VisorHydraService",
    functionName: "ProcessEnvelopes",
    parameters: new Dictionary<string, object?> { ... },
    connection, session, gatewayName,
    cancellationToken);
```

**Benefits:**
- Type-safe (parameters validated)
- HydraExecutionContext automatically managed
- Stats tracking
- Error handling

### XmcpClientHydraService Pattern

**VisorHydraService → XmcpClientHydraService:**
```csharp
// VisorHydraService.HandleMcp (Lines 253-262)
var xclient = _serviceProvider.GetRequiredService<XmcpClientHydraService>();
var result = await xclient.InvokeAsync("CallTool", new Dictionary<string, object?> {
    ["server"] = "http://localhost:5444",
    ["method"] = method,
    ["params"] = mcpParams
}, context, cancellationToken);
```

**Pattern:** Services can call other services via DI + InvokeAsync

## 10.5 Client Integration Points

### ChatGPTVisor → VisorGateway

**Connection:**
- ChatGPTVisor opens WebSocket to `ws://localhost:7777/visor`
- Sends JSON-serialized HydraEnvelope objects
- Receives JSON-serialized HydraEnvelope responses

**Flow:**
```
ChatGPT Browser
  → ChatGPTVisor (DevTools Protocol)
  → VISOR fence detection
  → Parse to HydraEnvelope
  → WebSocket.SendAsync(JSON)
  → VisorGateway receives
  → Deserialize to HydraEnvelope
  → Raise EnvelopesReceived event
```

### MCP Client → MCP Gateway

**Connection:**
- MCP client sends HTTP POST to `http://localhost:5080/mcp`
- Includes Bearer token (OAuth)
- Receives SSE stream for tool results

**Flow:**
```
MCP Client (e.g., Claude Desktop)
  → HTTP POST /mcp
  → Bearer token validation
  → MCP SDK parses request
  → Convert to HydraEnvelope
  → Invoke service
  → Convert result to MCP response
  → SSE stream back to client
```

## 10.6 Future Integration Points

### HTTP API Gateway
**Purpose:** REST API for HYDRA services
**Status:** Planned
**Endpoints:**
- `/api/services` - List services
- `/api/sessions` - Manage sessions
- `/api/invoke` - Direct service invocation

### gRPC Gateway
**Purpose:** High-performance RPC
**Status:** Future consideration
**Benefits:**
- Binary protocol (faster than JSON)
- Bidirectional streaming
- Code generation for clients

### GraphQL Gateway
**Purpose:** Flexible query API
**Status:** Future consideration
**Benefits:**
- Client-specified queries
- Single endpoint
- Type system

### MQTT Gateway
**Purpose:** IoT device integration
**Status:** Future consideration
**Benefits:**
- Lightweight protocol
- Pub/sub model
- QoS levels
