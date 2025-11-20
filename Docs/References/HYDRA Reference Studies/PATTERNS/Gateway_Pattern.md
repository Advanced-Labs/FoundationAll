# Gateway Pattern - Protocol-Agnostic Server Adapters

**Pattern Category:** Infrastructure / Adapter
**Complexity:** Medium
**Reusability:** High - applicable to any multi-protocol server platform on .NET

---

## Pattern Intent

Provide a uniform abstraction for different protocol servers (WebSocket, HTTP, SSE, stdio) with:
- Consistent lifecycle management (start/stop/restart)
- Automatic connection tracking and activity monitoring
- Thread-safe statistics collection
- Centralized registration and discovery
- Protocol-specific implementation isolation

## Problem Being Solved

When building a platform that needs to accept connections over multiple protocols:
- Each protocol has unique connection semantics (WebSocket frames, HTTP requests, stdio pipes)
- Connection lifecycle differs (persistent WebSocket vs stateless HTTP)
- Monitoring/stats should be uniform regardless of protocol
- Adding a new protocol should not impact existing gateways
- UI/management layer needs protocol-agnostic gateway enumeration

## HYDRA Implementation

### Core Abstraction

**File:** `Platform/Foundations/Gateway Foundation/GatewayServerBase.cs` (290 lines)

```csharp
public abstract class GatewayServerBase : IGatewayServer
{
    // Identity
    public string Name { get; }
    public int Port { get; }
    public string Protocol { get; }
    public string GatewayType { get; }

    // Lifecycle - template method pattern
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Set IsRunning, start stats timer, call OnStartAsync()
    }

    protected abstract Task OnStartAsync(CancellationToken cancellationToken);
    protected abstract Task OnStopAsync(CancellationToken cancellationToken);

    // Connection tracking - protected methods for derived classes
    protected void AddConnection(string connectionId)
    protected void RemoveConnection(string connectionId)
    protected void TouchConnection(string connectionId) // Activity timestamp

    // Stats tracking - protected methods
    protected void IncrementRequests()
    protected void IncrementReplies()
    protected void IncrementErrors()
    protected void IncrementEvents()

    // Event for monitoring
    public event EventHandler<GatewayStats>? StatsUpdated;
}
```

**Key Responsibilities:**
- Lines 68-129: Lifecycle orchestration with abstract hooks
- Lines 189-232: Connection tracking with activity timestamps
- Lines 146-183: Thread-safe stats increments
- Lines 238-271: Auto-detection of inactive connections (15-second timeout)

### Gateway Registry

**File:** `Platform/Foundations/Gateway Foundation/GatewayRegistry.cs` (141 lines)

```csharp
public sealed class GatewayRegistry
{
    private readonly Dictionary<string, IGatewayServer> _gateways = new();

    public void Register(IGatewayServer gateway) // Thread-safe with lock
    public void Unregister(string name)
    public IGatewayServer? Get(string name)
    public IReadOnlyList<IGatewayServer> GetAll()

    public async Task StartAllAsync(CancellationToken cancellationToken)
    public async Task StopAllAsync(CancellationToken cancellationToken)

    // Events
    public event EventHandler<IGatewayServer>? GatewayRegistered;
    public event EventHandler<string>? GatewayUnregistered;
}
```

**Responsibilities:**
- Centralized gateway lifecycle management
- Discovery interface for UI/management
- Prevents duplicate gateway names (lines 43-46)

### Example: VisorGateway (WebSocket)

**File:** `Platform/Core Gateways/VisorGateway/VisorGateway.cs` (384 lines)

```csharp
public sealed class VisorGateway : GatewayServerBase
{
    public VisorGateway(IDeterministicWriter deterministicWriter, int port = 7777)
        : base("Visor Gateway", port, "WebSocket-Visor", "Core") { }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Lines 70-121: Setup WebSocket listener on /visor endpoint
        // Line 116: Initialize pulse scheduler
    }

    private async Task HandleConnection(WebSocket webSocket, string connectionId)
    {
        AddConnection(connectionId); // Base class tracking

        var queue = new VisorConnectionQueue(connectionId); // Per-connection queue
        _connectionQueues[connectionId] = queue;

        // Start sender/receiver tasks concurrently
        var sendTask = SendLoop(webSocket, connectionId, queue);
        var receiveTask = ReceiveLoop(webSocket, connectionId);

        await Task.WhenAny(sendTask, receiveTask);

        RemoveConnection(connectionId); // Base class cleanup
    }

    private async Task ReceiveLoop(WebSocket ws, string connectionId)
    {
        // Lines 235-316: Parse incoming messages
        // Lines 248-278: Detect JSON envelope vs VISOR fenced block
        // Lines 289-293: Extract credit from flow envelope
        // Lines 298-302: Raise EnvelopesReceived event for service dispatch
        TouchConnection(connectionId); // Update activity
        IncrementRequests(); // Stats
    }

    private async Task SendLoop(WebSocket ws, string connectionId, VisorConnectionQueue queue)
    {
        // Lines 318-373: Dequeue messages and send with credit checking
        // Uses deterministic JSON writer for consistent serialization
        IncrementReplies(); // Stats
    }
}
```

**Protocol-Specific Concerns:**
- WebSocket framing and keepalive
- Credit-based flow control (VisorConnectionQueue)
- Dual format detection (JSON vs fenced blocks)
- Pulse scheduling for delayed messages

### Example: HTTP REST Gateway

**File:** `Components/Gateways/HTTP-API Gateway/HydraApiGateway.cs`

```csharp
public sealed class HydraApiGateway : GatewayServerBase
{
    public HydraApiGateway() : base("Hydra REST API", 6669, "HTTP-REST", "API") { }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Setup HTTP endpoints:
        // POST /api/v1/services/call/{serviceName}/{functionName}
        // GET  /api/v1/services/stream/{serviceName}/{functionName}
    }

    private async Task HandleCall(HttpContext httpContext)
    {
        var connectionId = Guid.NewGuid().ToString(); // Per-request connection
        AddConnection(connectionId);

        try
        {
            IncrementRequests();
            // ... service invocation
            IncrementReplies();
        }
        catch
        {
            IncrementErrors();
            throw;
        }
        finally
        {
            RemoveConnection(connectionId);
        }
    }
}
```

**HTTP-Specific Concerns:**
- Request/response model (vs persistent connections)
- SSE for streaming responses
- Short-lived "connections" (per-request tracking)

---

## Pattern Structure

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│              GatewayRegistry (Singleton)                 │
│   - Centralized discovery and lifecycle                 │
│   - Register/Unregister gateways                        │
│   - StartAll/StopAll orchestration                      │
└──────────┬──────────────────────────────────────────────┘
           │
           │ Contains
           │
    ┌──────┼──────┬───────────┬──────────┐
    │      │      │           │          │
    v      v      v           v          v
┌─────────────┐ ┌──────┐ ┌─────────┐ ┌──────────┐
│VisorGateway │ │HTTP  │ │MCP-SSE  │ │Custom... │
│(WebSocket)  │ │REST  │ │Gateway  │ │         │
└─────────────┘ └──────┘ └─────────┘ └──────────┘
    │              │         │            │
    └──────────────┴─────────┴────────────┘
                   │
        All inherit from GatewayServerBase
                   │
        ┌──────────┴───────────┐
        │                      │
        v                      v
 Template Methods       Protected Utilities
 - OnStartAsync        - AddConnection
 - OnStopAsync         - RemoveConnection
                       - TouchConnection
                       - Increment* (stats)
```

### Interaction Flow

```
1. Registration (at startup):
   GatewayRegistry.Register(visorGateway)
   GatewayRegistry.Register(httpGateway)
   GatewayRegistry.Register(mcpGateway)

2. Startup:
   GatewayRegistry.StartAllAsync()
     ├─> visorGateway.StartAsync()  → OnStartAsync() [WebSocket setup]
     ├─> httpGateway.StartAsync()   → OnStartAsync() [HTTP endpoints]
     └─> mcpGateway.StartAsync()    → OnStartAsync() [SSE setup]

3. Connection arrives (VisorGateway example):
   WebSocket connection → HandleConnection()
     ├─> AddConnection(connectionId)        [Base class tracking]
     ├─> Create VisorConnectionQueue
     ├─> Start SendLoop + ReceiveLoop
     └─> On disconnect: RemoveConnection()

4. Message received:
   ReceiveLoop()
     ├─> TouchConnection(connectionId)      [Activity timestamp]
     ├─> IncrementRequests()                [Stats]
     └─> Raise EnvelopesReceived event      [To service layer]

5. Monitoring:
   Base class timer (every 5 seconds):
     ├─> CheckInactiveConnections()         [15-second timeout]
     └─> Raise StatsUpdated event           [For UI dashboard]
```

---

## Key Design Decisions

### 1. Template Method Pattern
**Decision:** Base class controls lifecycle flow, derived classes implement protocol hooks

**Rationale:**
- Ensures consistent startup/shutdown sequence
- Centralizes common error handling
- Makes adding new gateways predictable

**Implementation:** Lines 68-129 of GatewayServerBase.cs

### 2. Connection Activity Tracking
**Decision:** Auto-detect inactive connections with 15-second timeout

**Rationale:**
- WebSocket connections can silently die (no TCP FIN)
- UI needs accurate "active vs total" connection counts
- Prevents connection leak on client crashes

**Implementation:**
- `TouchConnection()` updates timestamp (line 207 of VisorGateway.cs)
- Timer checks timestamps every 5 seconds (lines 238-271 of GatewayServerBase.cs)
- `ActiveConnections` property filters by 15-second threshold

### 3. Thread-Safe Stats
**Decision:** Lock-based synchronization for stats increments

**Rationale:**
- Multiple connections update stats concurrently
- Stats reads happen from UI thread
- Simple locks sufficient (low contention, fast operations)

**Implementation:** Lines 146-183 of GatewayServerBase.cs

```csharp
private readonly object _statsLock = new();
protected void IncrementRequests()
{
    lock (_statsLock)
    {
        _stats.IncrementRequests();
        RaiseStatsUpdated();
    }
}
```

### 4. Per-Connection Queues (VisorGateway)
**Decision:** Each WebSocket connection has its own outbound queue

**Rationale:**
- Credit-based flow control is per-connection
- Prevents head-of-line blocking between connections
- Supports priority scheduling per connection

**Implementation:** VisorConnectionQueue.cs (158 lines)

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define IGatewayServer Interface

```csharp
public interface IGatewayServer
{
    string Name { get; }
    int Port { get; }
    string Protocol { get; }
    bool IsRunning { get; }
    GatewayStats Stats { get; }

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task RestartAsync(CancellationToken cancellationToken);

    event EventHandler<GatewayStats>? StatsUpdated;
}
```

### Step 2: Implement GatewayServerBase

```csharp
public abstract class GatewayServerBase : IGatewayServer
{
    private readonly ConcurrentDictionary<string, DateTime> _connectionActivity = new();
    private readonly object _statsLock = new();
    private GatewayStats _stats = new();
    private System.Timers.Timer? _activityCheckTimer;

    protected GatewayServerBase(string name, int port, string protocol, string type)
    {
        Name = name;
        Port = port;
        Protocol = protocol;
        GatewayType = type;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return;

        _stats.MarkStarted();
        IsRunning = true;

        // Start activity checker
        _activityCheckTimer = new System.Timers.Timer(5000);
        _activityCheckTimer.Elapsed += (s, e) => CheckInactiveConnections();
        _activityCheckTimer.Start();

        await OnStartAsync(cancellationToken);
    }

    protected abstract Task OnStartAsync(CancellationToken cancellationToken);
    protected abstract Task OnStopAsync(CancellationToken cancellationToken);

    protected void AddConnection(string id)
    {
        _connectionActivity[id] = DateTime.UtcNow;
        lock (_statsLock) _stats.IncrementTotalConnections();
    }

    protected void TouchConnection(string id)
    {
        _connectionActivity[id] = DateTime.UtcNow;
    }

    private void CheckInactiveConnections()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-15);
        var active = _connectionActivity.Count(kvp => kvp.Value >= cutoff);
        lock (_statsLock) _stats.SetActiveConnections(active);
    }

    // ... stats methods, etc.
}
```

### Step 3: Implement Protocol-Specific Gateway

```csharp
public sealed class MyWebSocketGateway : GatewayServerBase
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();

    public MyWebSocketGateway(int port = 8080)
        : base("My WebSocket Gateway", port, "WebSocket", "Core") { }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        // Setup WebSocket listener
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _ = HandleConnection(wsContext.WebSocket);
            }
        }
    }

    private async Task HandleConnection(WebSocket ws)
    {
        var id = Guid.NewGuid().ToString();
        AddConnection(id);
        _connections[id] = ws;

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var buffer = new byte[4096];
                var result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                TouchConnection(id); // Update activity
                IncrementRequests();

                // Process message...
            }
        }
        finally
        {
            RemoveConnection(id);
            _connections.TryRemove(id, out _);
        }
    }
}
```

### Step 4: Create Gateway Registry

```csharp
public sealed class GatewayRegistry
{
    private readonly Dictionary<string, IGatewayServer> _gateways = new();
    private readonly object _lock = new();

    public void Register(IGatewayServer gateway)
    {
        lock (_lock)
        {
            if (_gateways.ContainsKey(gateway.Name))
                throw new InvalidOperationException($"Gateway '{gateway.Name}' already registered");

            _gateways[gateway.Name] = gateway;
        }
    }

    public async Task StartAllAsync(CancellationToken cancellationToken)
    {
        foreach (var gateway in _gateways.Values)
        {
            await gateway.StartAsync(cancellationToken);
        }
    }
}
```

### Step 5: Register and Start Gateways

```csharp
// At application startup
var registry = new GatewayRegistry();

registry.Register(new MyWebSocketGateway(port: 8080));
registry.Register(new MyHttpGateway(port: 8081));
registry.Register(new MyMcpGateway(port: 8082));

await registry.StartAllAsync(cancellationToken);
```

---

## Tradeoffs & Constraints

### Advantages
✅ Uniform abstraction across diverse protocols
✅ Easy to add new gateway types without changing existing code
✅ Built-in monitoring and health tracking
✅ Consistent lifecycle management
✅ Centralized discovery for management UIs

### Limitations
⚠️ Base class couples lifecycle + stats (could be separated with composition)
⚠️ 15-second activity timeout is hardcoded (could be configurable)
⚠️ Lock-based stats may become bottleneck at extreme scale (unlikely for HYDRA's use case)
⚠️ Per-connection queues increase memory footprint (trade memory for flow control)

### When NOT to Use This Pattern
❌ Single-protocol systems (unnecessary abstraction)
❌ Extremely high-throughput scenarios where lock-free stats are critical
❌ Protocols with complex multi-stage handshakes (pattern assumes simple start/stop)

---

## Related Patterns

- **Adapter Pattern:** Gateways adapt external protocols to internal envelope format
- **Observer Pattern:** StatsUpdated event for monitoring
- **Template Method:** Base class lifecycle control with abstract hooks
- **Registry Pattern:** Centralized gateway discovery

---

## Gen2 Evolution Notes

**Current (Gen1):** Gateways are in-process, direct CLR calls to services

**Future (Gen2):**
- Gateways may become Akka.NET actors for location transparency
- Stats collection may move to EventStoreDB event streams
- Per-connection state may be externalized to distributed cache
- Gateway registration may use service discovery (Consul, etc.)

**Migration Strategy:**
- Keep IGatewayServer interface stable
- Internal implementation can shift to actor model
- Registry becomes actor system's receptionist pattern

---

**Last Updated:** 2025-11-10
**Pattern Stability:** High - core abstraction unlikely to change in Gen2
**Code References:** GatewayServerBase.cs:18-290, GatewayRegistry.cs:1-141, VisorGateway.cs:1-384
